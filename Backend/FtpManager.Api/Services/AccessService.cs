using FtpManager.Api.Models;
using LiteDB;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace FtpManager.Api.Services
{
    public class AccessService
    {
        private readonly string _dbFilePath;
        private readonly object _lock = new();

        public AccessService()
        {
            var dbDir = Path.Combine(Directory.GetCurrentDirectory(), "logs", "database");
            Directory.CreateDirectory(dbDir);
            var databasePath = Path.Combine(dbDir, "ftp_manager.db");
            _dbFilePath = $"Filename={databasePath};Connection=shared";
            Initialize();
        }

        private void Initialize()
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var roles = db.GetCollection<AppRole>("roles");
                var users = db.GetCollection<AppUser>("users");
                roles.EnsureIndex(x => x.Name, true);
                users.EnsureIndex(x => x.Username, true);

                if (!roles.Exists(x => x.Name == "Admin"))
                {
                    roles.Insert(new AppRole
                    {
                        Id = "admin",
                        Name = "Admin",
                        Description = "Uygulamanin butun islemlerine erisebilen sistem rolu.",
                        Permissions = PermissionKeys.All.ToList(),
                        IsSystem = true
                    });
                }

                if (!roles.Exists(x => x.Name == "Personel"))
                {
                    roles.Insert(new AppRole
                    {
                        Id = "staff",
                        Name = "Personel",
                        Description = "Dosyalari gorebilir, indirebilir ve yukleyebilir.",
                        Permissions = new List<string>
                        {
                            PermissionKeys.FilesView,
                            PermissionKeys.FilesUpload,
                            PermissionKeys.FilesDownload,
                            PermissionKeys.ServersView
                        },
                        IsSystem = true
                    });
                }

                var legacyAdmin = users.FindOne(x => x.Username == "admin");
                if (legacyAdmin != null && VerifyPassword("admin123", legacyAdmin.PasswordHash, legacyAdmin.PasswordSalt))
                {
                    legacyAdmin.MustChangePassword = true;
                    users.Update(legacyAdmin);
                }
            }
        }

        public LoginResponse Login(string username, string password)
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var users = db.GetCollection<AppUser>("users");
                var sessions = db.GetCollection<UserSession>("sessions");
                var user = users.FindOne(x => x.Username == username);

                if (user == null || !user.IsActive || !VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
                {
                    throw new UnauthorizedAccessException("Kullanici adi veya sifre hatali.");
                }

                sessions.DeleteMany(x => x.UserId == user.Id || x.ExpiresAt < DateTime.Now);
                var session = new UserSession { UserId = user.Id };
                sessions.Insert(session);

                return new LoginResponse
                {
                    Token = session.Token,
                    User = ToCurrentUser(db, user)
                };
            }
        }

        public bool RequiresInitialSetup()
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                return db.GetCollection<AppUser>("users").Count() == 0;
            }
        }

        public LoginResponse CompleteInitialSetup(InitialSetupRequest request)
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var users = db.GetCollection<AppUser>("users");
                if (users.Count() != 0) throw new InvalidOperationException("Ilk kurulum zaten tamamlanmis.");
                var username = ValidateUsername(request.Username);
                ValidatePassword(request.Password);
                var (hash, salt) = HashPassword(request.Password);
                var user = new AppUser { Id = "admin", FullName = string.IsNullOrWhiteSpace(request.FullName) ? "Sistem Yoneticisi" : request.FullName.Trim(), Username = username, PasswordHash = hash, PasswordSalt = salt, RoleId = "admin", IsActive = true };
                users.Insert(user);
                var session = new UserSession { UserId = user.Id };
                db.GetCollection<UserSession>("sessions").Insert(session);
                return new LoginResponse { Token = session.Token, User = ToCurrentUser(db, user) };
            }
        }

        public CurrentUserDto ChangeOwnPassword(HttpContext context, ChangePasswordRequest request)
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var token = ReadToken(context);
                var session = db.GetCollection<UserSession>("sessions").FindOne(x => x.Token == token);
                var users = db.GetCollection<AppUser>("users");
                var user = session == null ? null : users.FindOne(x => x.Id == session.UserId);
                if (user == null || session!.ExpiresAt < DateTime.Now || !VerifyPassword(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                    throw new UnauthorizedAccessException("Mevcut sifre dogrulanamadi.");
                ValidatePassword(request.NewPassword);
                var (hash, salt) = HashPassword(request.NewPassword);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
                user.MustChangePassword = false;
                users.Update(user);
                return ToCurrentUser(db, user);
            }
        }

        public CurrentUserDto GetCurrentUser(HttpContext context)
        {
            var token = ReadToken(context);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new UnauthorizedAccessException("Oturum bilgisi bulunamadi.");
            }

            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var sessions = db.GetCollection<UserSession>("sessions");
                var users = db.GetCollection<AppUser>("users");
                var session = sessions.FindOne(x => x.Token == token);

                if (session == null || session.ExpiresAt < DateTime.Now)
                {
                    if (session != null) sessions.DeleteMany(x => x.Token == session.Token);
                    throw new UnauthorizedAccessException("Oturum gecersiz veya suresi dolmus.");
                }

                var user = users.FindOne(x => x.Id == session.UserId);
                if (user == null || !user.IsActive)
                {
                    throw new UnauthorizedAccessException("Kullanici aktif degil.");
                }

                return ToCurrentUser(db, user);
            }
        }

        public CurrentUserDto RequirePermission(HttpContext context, string permission)
        {
            var user = GetCurrentUser(context);
            if (user.MustChangePassword) throw new UnauthorizedAccessException("Devam etmeden once parolanizi degistirin.");
            if (!user.Permissions.Contains(permission))
            {
                throw new UnauthorizedAccessException("Bu islem icin yetkiniz yok.");
            }
            return user;
        }

        public bool HasPermission(HttpContext context, string permission)
        {
            try
            {
                return GetCurrentUser(context).Permissions.Contains(permission);
            }
            catch
            {
                return false;
            }
        }

        public List<AppRole> GetRoles()
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                return db.GetCollection<AppRole>("roles").FindAll().OrderBy(x => x.Name).ToList();
            }
        }

        public AppRole CreateRole(SaveRoleRequest request)
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var roles = db.GetCollection<AppRole>("roles");
                var role = new AppRole
                {
                    Name = request.Name.Trim(),
                    Description = request.Description.Trim(),
                    Permissions = request.Permissions.Intersect(PermissionKeys.All).Distinct().ToList()
                };
                roles.Insert(role);
                return role;
            }
        }

        public AppRole UpdateRole(string id, SaveRoleRequest request)
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var roles = db.GetCollection<AppRole>("roles");
                var role = roles.FindOne(x => x.Id == id) ?? throw new KeyNotFoundException("Rol bulunamadi.");
                role.Name = role.IsSystem && role.Id == "admin" ? "Admin" : request.Name.Trim();
                role.Description = request.Description.Trim();
                role.Permissions = role.Id == "admin"
                    ? PermissionKeys.All.ToList()
                    : request.Permissions.Intersect(PermissionKeys.All).Distinct().ToList();
                roles.Update(role);
                return role;
            }
        }

        public void DeleteRole(string id)
        {
            if (id == "admin")
            {
                throw new InvalidOperationException("Admin rolu silinemez.");
            }

            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var users = db.GetCollection<AppUser>("users");
                if (users.Exists(x => x.RoleId == id))
                {
                    throw new InvalidOperationException("Bu role bagli kullanicilar oldugu icin rol silinemez.");
                }
                db.GetCollection<AppRole>("roles").Delete(id);
            }
        }

        public List<UserDto> GetUsers()
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var rolesById = db.GetCollection<AppRole>("roles")
                    .FindAll()
                    .ToDictionary(x => x.Id, x => x.Name);

                return db.GetCollection<AppUser>("users")
                    .FindAll()
                    .Select(user => new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Username = user.Username,
                        RoleId = user.RoleId,
                        RoleName = rolesById.GetValueOrDefault(user.RoleId, "Rolsuz"),
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    })
                    .OrderBy(x => x.FullName)
                    .ToList();
            }
        }

        public UserDto CreateUser(SaveUserRequest request)
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var users = db.GetCollection<AppUser>("users");
                var username = ValidateUsername(request.Username);
                if (users.Exists(x => x.Username == username)) throw new InvalidOperationException("Bu kullanici adi zaten kullaniliyor.");
                ValidatePassword(request.Password);
                var (hash, salt) = HashPassword(request.Password);
                var user = new AppUser { FullName = request.FullName.Trim(), Username = username, PasswordHash = hash, PasswordSalt = salt, RoleId = request.RoleId, IsActive = request.IsActive };
                users.Insert(user);
                return ToUserDto(db, user);
            }
        }

        public UserDto UpdateUser(string id, SaveUserRequest request)
        {
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                var users = db.GetCollection<AppUser>("users");
                var user = users.FindOne(x => x.Id == id) ?? throw new KeyNotFoundException("Kullanici bulunamadi.");
                user.FullName = request.FullName.Trim();
                user.Username = request.Username.Trim();
                user.RoleId = request.RoleId;
                user.IsActive = request.IsActive;
                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    ValidatePassword(request.Password);
                    var (hash, salt) = HashPassword(request.Password);
                    user.PasswordHash = hash;
                    user.PasswordSalt = salt;
                }
                users.Update(user);
                return ToUserDto(db, user);
            }
        }

        public void DeleteUser(string id)
        {
            if (id == "admin")
            {
                throw new InvalidOperationException("Varsayilan admin kullanicisi silinemez.");
            }

            lock (_lock)
            {
                using var db = new LiteDatabase(_dbFilePath);
                db.GetCollection<AppUser>("users").Delete(id);
                db.GetCollection<UserSession>("sessions").DeleteMany(x => x.UserId == id);
            }
        }

        private static CurrentUserDto ToCurrentUser(LiteDatabase db, AppUser user)
        {
            var role = db.GetCollection<AppRole>("roles").FindOne(x => x.Id == user.RoleId);
            return new CurrentUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                RoleId = user.RoleId,
                RoleName = role?.Name ?? "Rolsuz",
                Permissions = role?.Permissions ?? new List<string>(),
                MustChangePassword = user.MustChangePassword
            };
        }

        private static UserDto ToUserDto(LiteDatabase db, AppUser user)
        {
            var role = db.GetCollection<AppRole>("roles").FindOne(x => x.Id == user.RoleId);
            return new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                RoleId = user.RoleId,
                RoleName = role?.Name ?? "Rolsuz",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };
        }

        private static string ReadToken(HttpContext context)
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader["Bearer ".Length..].Trim();
            }
            return context.Request.Headers["X-App-Token"].ToString();
        }

        private static (string Hash, string Salt) HashPassword(string password)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(16);
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        private static void ValidatePassword(string password)
        {
            if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
                throw new InvalidOperationException("Sifre en az 12 karakter olmali; buyuk harf, kucuk harf, rakam ve sembol icermelidir.");
        }

        private static string ValidateUsername(string username)
        {
            var value = username.Trim();
            if (value.Length is < 3 or > 64 || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')))
                throw new InvalidOperationException("Kullanici adi 3-64 karakter olmali ve sadece harf, rakam, nokta, tire veya alt cizgi icermelidir.");
            return value;
        }

        private static bool VerifyPassword(string password, string hash, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hashBytes, Convert.FromBase64String(hash));
        }
    }
}
