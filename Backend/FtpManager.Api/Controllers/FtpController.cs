using FtpManager.Api.Services;
using FtpManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FtpManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FtpController : ControllerBase
    {
        private const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;
        private const long MaxChunkBytes = 32L * 1024 * 1024;
        private const int MaxChunks = 512;
        private readonly FtpService _ftpService;
        private readonly ILogService _customLogger;
        private readonly LocalFtpServer _localFtpServer;
        private readonly AccessService _accessService;
        private readonly NgrokTunnelService _ngrokTunnelService;
        private readonly TrashService _trashService;

        public FtpController(
            FtpService ftpService,
            ILogService customLogger,
            LocalFtpServer localFtpServer,
            AccessService accessService,
            NgrokTunnelService ngrokTunnelService,
            TrashService trashService)
        {
            _ftpService = ftpService;
            _customLogger = customLogger;
            _localFtpServer = localFtpServer;
            _accessService = accessService;
            _ngrokTunnelService = ngrokTunnelService;
            _trashService = trashService;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] string path = "/")
        {
            var denial = DenyUnless(PermissionKeys.FilesView);
            if (denial is not null) return denial;
            try
            {
                var items = await _ftpService.GetListAsync(path);
                return Ok(path == "/" ? items.Where(item => !string.Equals(item.Name, ".ftp-manager-trash", StringComparison.OrdinalIgnoreCase)) : items);
            }
            catch (Exception ex)
            {
                _customLogger.LogError("FTP_LIST", $"Dizin listelenirken hata: {path}", ex);
                return StatusCode(500, $"Dizin listelenemedi: {ex.Message}");
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string currentPath)
        {
            var denial = DenyUnless(PermissionKeys.FilesUpload);
            if (denial is not null) return denial;
            if (file == null || file.Length == 0)
                return BadRequest("Geçersiz dosya.");

            if (file.Length > MaxUploadBytes) return BadRequest("Dosya 2 GB sinirini asiyor.");

            try
            {
                string remotePath = Path.Combine(currentPath ?? "/", file.FileName).Replace("\\", "/");
                using var stream = file.OpenReadStream();
                var isUploaded = await _ftpService.UploadFileAsync(stream, remotePath);

                if (isUploaded)
                {
                    _customLogger.LogInfo("DOSYA_YUKLE", $"Dosya doğrudan yüklendi: {remotePath}");
                    return Ok(new { message = "Dosya başarıyla yüklendi." });
                }
                
                return StatusCode(500, "Dosya FTP sunucusuna yazılırken başarısız oldu.");
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_YUKLE", $"Standart yükleme hatası: {file.FileName}", ex);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("upload-chunk")]
        public async Task<IActionResult> UploadChunk(
            IFormFile file,
            [FromForm] string uploadId,
            [FromForm] int chunkIndex,
            [FromForm] int totalChunks,
            [FromForm] string fileName,
            [FromForm] string currentPath)
        {
            var denial = DenyUnless(PermissionKeys.FilesUpload);
            if (denial is not null) return denial;
            if (file == null || file.Length == 0)
                return BadRequest("Geçersiz chunk.");
            if (string.IsNullOrEmpty(uploadId))
                return BadRequest("Geçersiz uploadId.");

            if (!IsSafeUploadId(uploadId) || chunkIndex < 0 || totalChunks is < 1 or > MaxChunks || chunkIndex >= totalChunks)
                return BadRequest("Gecersiz parca yukleme bilgisi.");
            if (file.Length > MaxChunkBytes) return BadRequest("Parca boyutu 32 MB sinirini asiyor.");

            try
            {
                var tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", uploadId);
                if (!Directory.Exists(tempDirectory))
                {
                    Directory.CreateDirectory(tempDirectory);
                }

                var chunkFilePath = Path.Combine(tempDirectory, $"{chunkIndex}.part");
                using (var chunkStream = new FileStream(chunkFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(chunkStream);
                }

                // Tüm parçaların gelip gelmediğini kontrol etme
                bool allChunksReceived = true;
                for (int i = 0; i < totalChunks; i++)
                {
                    if (!System.IO.File.Exists(Path.Combine(tempDirectory, $"{i}.part")))
                    {
                        allChunksReceived = false;
                        break;
                    }
                }

                if (allChunksReceived)
                {
                    var finalTempFile = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", $"{uploadId}_merged.tmp");
                    bool isUploaded = false;

                    try
                    {
                        // Parçaları güvenli bir şekilde birleştir
                        using (var destinationStream = new FileStream(finalTempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            for (int i = 0; i < totalChunks; i++)
                            {
                                var partPath = Path.Combine(tempDirectory, $"{i}.part");
                                using (var partStream = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    await partStream.CopyToAsync(destinationStream);
                                }
                            }
                        }

                        // FTP'ye yükle
                        string remotePath = Path.Combine(currentPath ?? "/", fileName).Replace("\\", "/");
                        using (var mergedStream = new FileStream(finalTempFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            isUploaded = await _ftpService.UploadFileAsync(mergedStream, remotePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _customLogger.LogError("CHUNK_BIRLESTIRME", $"Dosya birleştirilirken veya FTP'ye aktarılırken hata oluştu. ID: {uploadId}", ex);
                        throw;
                    }
                    finally
                    {
                        // Geçici dosyaları temizleme
                        try
                        {
                            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
                            if (System.IO.File.Exists(finalTempFile)) System.IO.File.Delete(finalTempFile);
                        }
                        catch (Exception ex)
                        {
                            _customLogger.LogError("TEMIZLIK_HATASI", $"Geçici dosyalar silinemedi: {ex.Message}", ex);
                        }
                    }

                    if (isUploaded)
                    {
                        _customLogger.LogInfo("DOSYA_YUKLE", $"Büyük dosya parçalı olarak başarıyla yüklendi: {fileName}");
                        return Ok(new { message = "Dosya başarıyla birleştirildi ve FTP'ye yüklendi." });
                    }
                    
                    return StatusCode(500, "Dosya birleştirildi fakat FTP sunucusuna aktarılamadı.");
                }

                return Ok(new { message = $"Chunk {chunkIndex + 1}/{totalChunks} başarıyla yüklendi." });
            }
            catch (Exception ex)
            {
                _customLogger.LogError("CHUNK_YUKLE", $"Chunk yükleme hatası Index: {chunkIndex}, ID: {uploadId}", ex);
                return StatusCode(500, $"Parça yükleme başarısız: {ex.Message}");
            }
        }

        [HttpPost("cancel-upload")]
        public IActionResult CancelUpload([FromQuery] string uploadId)
        {
            var denial = DenyUnless(PermissionKeys.FilesUpload);
            if (denial is not null) return denial;
            if (!IsSafeUploadId(uploadId)) return BadRequest("Gecersiz uploadId.");
            if (string.IsNullOrEmpty(uploadId))
                return BadRequest("Geçersiz uploadId.");

            try
            {
                var tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", uploadId);
                var finalTempFile = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", $"{uploadId}_merged.tmp");

                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
                if (System.IO.File.Exists(finalTempFile)) System.IO.File.Delete(finalTempFile);

                _customLogger.LogWarning("DOSYA_YUKLE", $"Parçalı yükleme iptal edildi. UploadId: {uploadId}");
                return Ok(new { message = "Yükleme başarıyla iptal edildi." });
            }
            catch (Exception ex)
            {
                _customLogger.LogError("IPTAL_HATASI", $"İptal işlemi sırasında hata: {uploadId}", ex);
                return StatusCode(500, $"İptal işlemi sırasında hata oluştu: {ex.Message}");
            }
        }

        [HttpPost("rename")]
        public async Task<IActionResult> Rename([FromQuery] string sourcePath, [FromQuery] string targetPath)
        {
            var denial = DenyUnless(PermissionKeys.FilesModify);
            if (denial is not null) return denial;
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
                return BadRequest("Geçersiz kaynak veya hedef yol.");

            try
            {
                // URL Decode işlemi (Özel karakterlerin doğru çözümlenmesi için zorunlu)
                string decodedSource = Uri.UnescapeDataString(sourcePath);
                string decodedTarget = Uri.UnescapeDataString(targetPath);

                var success = await _ftpService.RenameAsync(decodedSource, decodedTarget);
                if (success)
                {
                    _customLogger.LogInfo("DOSYA_TASIMA", $"Öğe taşındı: {decodedSource} -> {decodedTarget}");
                    return Ok(new { message = "Öğe başarıyla taşındı/yeniden adlandırıldı." });
                }
                
                _customLogger.LogWarning("DOSYA_TASIMA", $"Öğe taşınamadı (Sunucu reddetti): {decodedSource}");
                return StatusCode(500, "FTP Sunucusu taşıma işlemini gerçekleştiremedi. Yol doğruluğunu kontrol edin.");
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_TASIMA", $"Taşıma esnasında kritik hata. Kaynak: {sourcePath}", ex);
                return StatusCode(500, $"Yeniden adlandırma hatası: {ex.Message}");
            }
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string remotePath)
        {
            var denial = DenyUnless(PermissionKeys.FilesDownload);
            if (denial is not null) return denial;
            try
            {
                string decodedPath = Uri.UnescapeDataString(remotePath);
                var stream = await _ftpService.DownloadFileAsync(decodedPath);
                var contentType = Path.GetExtension(decodedPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf"
                    : "application/octet-stream";
                return File(stream, contentType);
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_INDIR", $"İndirme hatası: {remotePath}", ex);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteItem([FromQuery] string path, [FromQuery] bool isFolder)
        {
            var denial = DenyUnless(PermissionKeys.FilesModify);
            if (denial is not null) return denial;
            try
            {
                string decodedPath = Uri.UnescapeDataString(path);
                await _trashService.MoveToTrashAsync(_ftpService, GetSelectedServerId(), decodedPath, isFolder);
                _customLogger.LogInfo("DOSYA_SIL", $"Öğe silindi: {decodedPath} (Klasör mü: {isFolder})");
                return Ok(new { message = "Öge başarıyla silindi." });
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_SIL", $"Silme hatası: {path}", ex);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("create-folder")]
        public async Task<IActionResult> CreateFolder([FromQuery] string path)
        {
            var denial = DenyUnless(PermissionKeys.FilesModify);
            if (denial is not null) return denial;
            try
            {
                string decodedPath = Uri.UnescapeDataString(path);
                await _ftpService.CreateDirectoryAsync(decodedPath);
                return Ok(new { message = "Klasör başarıyla oluşturuldu." });
            }
            catch (Exception ex)
            {
                _customLogger.LogError("KLASOR_OLUSTUR", $"Klasör oluşturulamadı: {path}", ex);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("trash")]
        public IActionResult GetTrash()
        {
            var denial = DenyUnless(PermissionKeys.FilesModify);
            if (denial is not null) return denial;
            return Ok(_trashService.Get(GetSelectedServerId()));
        }

        [HttpPost("trash/{id}/restore")]
        public async Task<IActionResult> RestoreTrash(string id)
        {
            var denial = DenyUnless(PermissionKeys.FilesModify);
            if (denial is not null) return denial;
            try
            {
                await _trashService.RestoreAsync(_ftpService, GetSelectedServerId(), id);
                return Ok(new { message = "Oge ozgun konumuna geri yuklendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromQuery] string username)
        {
            var denial = DenyUnless(PermissionKeys.FilesView);
            if (denial is not null) return denial;
            try
            {
                var isSuccess = await _ftpService.VerifyCredentialsAsync();
                if (!isSuccess)
                {
                    return BadRequest("Bağlantı başarısız. Kullanıcı adı veya şifre hatalı.");
                }

                _customLogger.LogInfo("KULLANICI_GIRIS", $"Kullanici basariyla giris yapti: {username}");
                return Ok(new { message = "Giriş başarılı." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("logs/file")]
        public async Task<IActionResult> GetFileLogs()
        {
            var denial = DenyUnless(PermissionKeys.LogsView);
            if (denial is not null) return denial;
            try
            {
                var logs = await _customLogger.GetJsonLogsAsync();
                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("logs/database")]
        public async Task<IActionResult> GetDatabaseLogs()
        {
            var denial = DenyUnless(PermissionKeys.LogsView);
            if (denial is not null) return denial;
            try
            {
                var logs = await _customLogger.GetDatabaseLogsAsync();
                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // --- FTP Server Configuration & Management Endpoints ---

        [HttpGet("servers")]
        public IActionResult GetServers()
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersView);
                var servers = _localFtpServer.GetServers();
                if (!_accessService.HasPermission(HttpContext, PermissionKeys.ServersCredentials))
                {
                    foreach (var server in servers)
                    {
                        server.Password = string.Empty;
                        server.SftpPassword = null;
                    }
                }
                return Ok(servers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("root-folders")]
        public IActionResult GetRootFolders()
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                return Ok(_localFtpServer.GetRootFolders());
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("servers")]
        public async Task<IActionResult> AddServer(
            [FromForm] FtpServerConfig config,
            [FromForm] IFormFile? certificate = null,
            [FromForm] IFormFile? privateKey = null)
        {
            string? certificatePath = null;
            string? privateKeyPath = null;
            var uploadedCertificatePaths = new List<string>();
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                var hasCertificate = certificate != null && certificate.Length > 0;
                var hasPrivateKey = privateKey != null && privateKey.Length > 0;
                if (hasCertificate != hasPrivateKey)
                {
                    return BadRequest("FTPS için .crt sertifikası ve .key özel anahtarı birlikte seçilmelidir.");
                }
                if (hasCertificate && (!certificate!.FileName.EndsWith(".crt", StringComparison.OrdinalIgnoreCase) ||
                    !privateKey!.FileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest("Sertifika .crt, özel anahtar .key uzantılı olmalıdır.");
                }

                config.Id = Guid.NewGuid().ToString();
                config.TlsEnabled = hasCertificate;
                if (hasCertificate)
                {
                    var certificateDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "certificates");
                    Directory.CreateDirectory(certificateDirectory);
                    certificatePath = Path.Combine(certificateDirectory, $"{config.Id}.crt");
                    privateKeyPath = Path.Combine(certificateDirectory, $"{config.Id}.key");
                    config.CertificatePath = certificatePath;
                    config.PrivateKeyPath = privateKeyPath;
                    uploadedCertificatePaths.Add(certificatePath);
                    uploadedCertificatePaths.Add(privateKeyPath);

                    await using (var certificateStream = System.IO.File.Create(certificatePath))
                    {
                        await certificate!.CopyToAsync(certificateStream);
                    }
                    await using (var privateKeyStream = System.IO.File.Create(privateKeyPath))
                    {
                        await privateKey!.CopyToAsync(privateKeyStream);
                    }

                }
                var created = _localFtpServer.AddServer(config);
                return Ok(created);
            }
            catch (Exception ex)
            {
                foreach (var uploadedCertificatePath in uploadedCertificatePaths)
                {
                    if (System.IO.File.Exists(uploadedCertificatePath)) System.IO.File.Delete(uploadedCertificatePath);
                }
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("backup")]
        public async Task<IActionResult> DownloadBackup()
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                var filename = $"ftp-manager-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
                var backupPath = await _localFtpServer.CreateBackupFileAsync(HttpContext.RequestAborted);
                Response.OnCompleted(() =>
                {
                    try { System.IO.File.Delete(backupPath); } catch { }
                    return Task.CompletedTask;
                });
                return File(new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan), "application/zip", filename);
            }
            catch (Exception ex)
            {
                _customLogger.LogError("YEDEK_INDIR", "Yedek ZIP hazirlanamadi.", ex);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("backup/restore")]
        public async Task<IActionResult> RestoreBackup(IFormFile backup)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                if (backup == null || backup.Length == 0 || backup.Length > MaxUploadBytes || !backup.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Gecerli bir .zip yedegi secin.");
                await using var stream = backup.OpenReadStream();
                await _localFtpServer.RestoreBackupAsync(stream);
                return Ok(new { message = "Yedek geri yuklendi; tanimli sunucular yeniden baslatildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("servers/{id}")]
        public IActionResult DeleteServer(string id)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                _localFtpServer.DeleteServer(id);
                return Ok(new { message = "Sunucu başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("servers/{id}/start")]
        public IActionResult StartServer(string id)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                _localFtpServer.StartServer(id);
                return Ok(new { message = "Sunucu başarıyla başlatıldı." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("servers/{id}/sftp")]
        public IActionResult ProvisionSftp(string id)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                var server = _localFtpServer.ProvisionSftp(id);
                if (!_accessService.HasPermission(HttpContext, PermissionKeys.ServersCredentials))
                {
                    server.Password = string.Empty;
                    server.SftpPassword = null;
                }
                return Ok(server);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("sftp/tunnel")]
        public async Task<IActionResult> GetSftpTunnel([FromQuery] int localPort = 2222)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersView);
                return Ok(await _ngrokTunnelService.GetStatusAsync(localPort, HttpContext.RequestAborted));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("servers/{id}/sftp/tunnel/start")]
        public async Task<IActionResult> StartSftpTunnel(string id)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                var server = _localFtpServer.GetServer(id) ?? throw new KeyNotFoundException("Sunucu bulunamadi.");
                if (!server.SftpEnabled)
                {
                    throw new InvalidOperationException("Once bu sunucu icin SFTP erisimini hazirlayin.");
                }

                return Ok(await _ngrokTunnelService.StartAsync(server.SftpLocalPort, HttpContext.RequestAborted));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("sftp/tunnel/stop")]
        public async Task<IActionResult> StopSftpTunnel([FromQuery] int localPort = 2222)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                return Ok(await _ngrokTunnelService.StopAsync(localPort, HttpContext.RequestAborted));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("servers/{id}/stop")]
        public async Task<IActionResult> StopServer(string id)
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.ServersManage);
                await _localFtpServer.StopServerAsync(id);
                return Ok(new { message = "Sunucu başarıyla durduruldu." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private IActionResult? DenyUnless(string permission)
        {
            try
            {
                var user = _accessService.GetCurrentUser(HttpContext);
                if (user.MustChangePassword)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Devam etmeden once parolanizi degistirin." });
                }
                if (!user.Permissions.Contains(permission))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bu islem icin yetkiniz yok." });
                }
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        private string GetSelectedServerId()
        {
            var id = HttpContext.Request.Headers["X-FTP-Server-Id"].ToString();
            return string.IsNullOrWhiteSpace(id) ? "default" : id;
        }

        private static bool IsSafeUploadId(string? uploadId) =>
            !string.IsNullOrWhiteSpace(uploadId) &&
            uploadId.Length is >= 16 and <= 80 &&
            uploadId.StartsWith("chunk_", StringComparison.Ordinal) &&
            uploadId.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
    }
}
