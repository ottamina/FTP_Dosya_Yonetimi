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
            _dbFilePath = Path.Combine(dbDir, "ftp_manager.db");
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

                if (users.Count() == 0)
                {
                    var (hash, salt) = HashPassword("admin123");
                    users.Insert(new AppUser
                    {
                        Id = "admin",
                        FullName = "Sistem Yoneticisi",
                        Username = "admin",
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        RoleId = "admin",
                        IsActive = true
                    });
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
                return db.GetCollection<AppUser>("users")
                    .FindAll()
                    .Select(x => ToUserDto(db, x))
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
                if (users.Exists(x => x.Username == request.Username.Trim()))
                {
                    throw new InvalidOperationException("Bu kullanici adi zaten kullaniliyor.");
                }

                var (hash, salt) = HashPassword(request.Password);
                var user = new AppUser
                {
                    FullName = request.FullName.Trim(),
                    Username = request.Username.Trim(),
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    RoleId = request.RoleId,
                    IsActive = request.IsActive
                };
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
                Permissions = role?.Permissions ?? new List<string>()
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

        private static bool VerifyPassword(string password, string hash, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hashBytes, Convert.FromBase64String(hash));
        }
    }
}
