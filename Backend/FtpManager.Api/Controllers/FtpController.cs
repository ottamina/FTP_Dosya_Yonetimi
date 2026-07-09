using FtpManager.Api.Services;
using FtpManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FtpManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FtpController : ControllerBase
    {
        private readonly FtpService _ftpService;
        private readonly ILogService _customLogger;
        private readonly LocalFtpServer _localFtpServer;

        public FtpController(FtpService ftpService, ILogService customLogger, LocalFtpServer localFtpServer)
        {
            _ftpService = ftpService;
            _customLogger = customLogger;
            _localFtpServer = localFtpServer;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] string path = "/")
        {
            try
            {
                var items = await _ftpService.GetListAsync(path);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string currentPath)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Geçersiz dosya.");

            try
            {
                string remotePath = Path.Combine(currentPath, file.FileName).Replace("\\", "/");
                using var stream = file.OpenReadStream();
                var isUploaded = await _ftpService.UploadFileAsync(stream, remotePath);

                if (isUploaded)
                    return Ok(new { message = "Dosya başarıyla yüklendi." });
                
                return StatusCode(500, "Dosya yüklenemedi.");
            }
            catch (Exception ex)
            {
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
            if (file == null || file.Length == 0)
                return BadRequest("Geçersiz chunk.");
            if (string.IsNullOrEmpty(uploadId))
                return BadRequest("Geçersiz uploadId.");

            try
            {
                var tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", uploadId);
                if (!Directory.Exists(tempDirectory))
                {
                    Directory.CreateDirectory(tempDirectory);
                }

                var chunkFilePath = Path.Combine(tempDirectory, $"{chunkIndex}.part");
                using (var chunkStream = new FileStream(chunkFilePath, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(chunkStream);
                }

                // Check if all chunks have been received
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
                        // Merge chunks into one file
                        using (var destinationStream = new FileStream(finalTempFile, FileMode.Create, FileAccess.Write))
                        {
                            for (int i = 0; i < totalChunks; i++)
                            {
                                var partPath = Path.Combine(tempDirectory, $"{i}.part");
                                using (var partStream = new FileStream(partPath, FileMode.Open, FileAccess.Read))
                                {
                                    await partStream.CopyToAsync(destinationStream);
                                }
                            }
                        }

                        // Upload merged file to FTP
                        string remotePath = Path.Combine(currentPath, fileName).Replace("\\", "/");
                        using (var mergedStream = new FileStream(finalTempFile, FileMode.Open, FileAccess.Read))
                        {
                            isUploaded = await _ftpService.UploadFileAsync(mergedStream, remotePath);
                        }
                    }
                    finally
                    {
                        // Clean up
                        try
                        {
                            if (Directory.Exists(tempDirectory))
                            {
                                Directory.Delete(tempDirectory, true);
                            }
                            if (System.IO.File.Exists(finalTempFile))
                            {
                                System.IO.File.Delete(finalTempFile);
                            }
                        }
                        catch (Exception ex)
                        {
                            _customLogger.LogError("DOSYA_YUKLE", $"Geçici dosyalar silinirken hata oluştu: {ex.Message}", ex);
                        }
                    }

                    if (isUploaded)
                    {
                        return Ok(new { message = "Dosya başarıyla birleştirildi ve FTP'ye yüklendi." });
                    }
                    
                    return StatusCode(500, "Dosya FTP sunucusuna yüklenemedi.");
                }

                return Ok(new { message = $"Chunk {chunkIndex + 1}/{totalChunks} başarıyla yüklendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("cancel-upload")]
        public IActionResult CancelUpload([FromQuery] string uploadId)
        {
            if (string.IsNullOrEmpty(uploadId))
                return BadRequest("Geçersiz uploadId.");

            try
            {
                var tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", uploadId);
                var finalTempFile = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", $"{uploadId}_merged.tmp");

                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
                if (System.IO.File.Exists(finalTempFile))
                {
                    System.IO.File.Delete(finalTempFile);
                }

                _customLogger.LogWarning("DOSYA_YUKLE", $"Parçalı yükleme iptal edildi ve geçici dosyalar temizlendi. UploadId: {uploadId}");
                return Ok(new { message = "Yükleme başarıyla iptal edildi ve geçici dosyalar temizlendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"İptal işlemi sırasında hata oluştu: {ex.Message}");
            }
        }

        [HttpPost("rename")]
        public async Task<IActionResult> Rename([FromQuery] string sourcePath, [FromQuery] string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
                return BadRequest("Geçersiz kaynak veya hedef yol.");

            try
            {
                var success = await _ftpService.RenameAsync(sourcePath, targetPath);
                if (success)
                    return Ok(new { message = "Öğe başarıyla taşındı/yeniden adlandırıldı." });
                
                return StatusCode(500, "Öğe taşınamadı.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string remotePath)
        {
            try
            {
                var stream = await _ftpService.DownloadFileAsync(remotePath);
                string fileName = Path.GetFileName(remotePath);
                return File(stream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteItem([FromQuery] string path, [FromQuery] bool isFolder)
        {
            try
            {
                await _ftpService.DeleteItemAsync(path, isFolder);
                return Ok(new { message = "Öge başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("create-folder")]
        public async Task<IActionResult> CreateFolder([FromQuery] string path)
        {
            try
            {
                await _ftpService.CreateDirectoryAsync(path);
                return Ok(new { message = "Klasör başarıyla oluşturuldu." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromQuery] string username)
        {
            try
            {
                var isSuccess = await _ftpService.VerifyCredentialsAsync();
                if (!isSuccess)
                {
                    return BadRequest("Bağlantı başarısız. Kullanıcı adı veya şifre hatalı.");
                }

                _customLogger.LogInfo("KULLANICI_GIRIS", $"Kullanici basariyla giris yapti: {username} (Isim: Yonetici, Rol: Admin)");
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
                var servers = _localFtpServer.GetServers();
                return Ok(servers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("servers")]
        public IActionResult AddServer([FromBody] FtpServerConfig config)
        {
            try
            {
                var created = _localFtpServer.AddServer(config);
                return Ok(created);
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
                _localFtpServer.StartServer(id);
                return Ok(new { message = "Sunucu başarıyla başlatıldı." });
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
                await _localFtpServer.StopServerAsync(id);
                return Ok(new { message = "Sunucu başarıyla durduruldu." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
