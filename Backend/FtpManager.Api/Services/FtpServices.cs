using FluentFTP;
using FtpManager.Api.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FtpManager.Api.Services
{
    public class FtpService
    {
        private readonly string _host;
        private readonly string _username;
        private readonly string _password;
        private readonly int _port;
        private readonly ILogService _customLogger;

        public FtpService(IConfiguration configuration, ILogService customLogger, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor, LocalFtpServer localFtpServer)
        {
            _customLogger = customLogger;

            var context = httpContextAccessor.HttpContext;
            if (context != null)
            {
                if (context.Request.Headers.TryGetValue("X-FTP-Server-Id", out var serverIdVal))
                {
                    string serverId = serverIdVal.ToString();
                    var config = localFtpServer.GetServer(serverId);
                    if (config != null)
                    {
                        _host = config.Host;
                        _port = config.Port;
                        _username = config.Username;
                        _password = config.Password;

                        // Override username/password if user sent them dynamically from UI
                        if (context.Request.Headers.TryGetValue("X-FTP-Username", out var customUser))
                        {
                            _username = customUser.ToString();
                        }
                        if (context.Request.Headers.TryGetValue("X-FTP-Password", out var customPass))
                        {
                            _password = customPass.ToString();
                        }
                        return;
                    }
                }

                if (context.Request.Headers.TryGetValue("X-FTP-Host", out var hVal) &&
                    context.Request.Headers.TryGetValue("X-FTP-Port", out var pVal) &&
                    context.Request.Headers.TryGetValue("X-FTP-Username", out var uVal) &&
                    context.Request.Headers.TryGetValue("X-FTP-Password", out var passVal))
                {
                    _host = hVal.ToString();
                    _port = int.TryParse(pVal.ToString(), out var p) ? p : 21;
                    _username = uVal.ToString();
                    _password = passVal.ToString();
                    return;
                }
            }

            _host = configuration["FtpSettings:Host"] ?? "127.0.0.1";
            _username = configuration["FtpSettings:Username"] ?? "ftpadmin";
            _password = configuration["FtpSettings:Password"] ?? "admin123";
            _port = int.TryParse(configuration["FtpSettings:Port"], out var p2) ? p2 : 2121;
        }

        private async Task<AsyncFtpClient> CreateClientAsync()
        {
            var client = new AsyncFtpClient(_host, _username, _password, _port);
            await client.Connect();
            return client;
        }

        public async Task<List<FtpItemDto>> GetListAsync(string path)
        {
            try
            {
                using var client = await CreateClientAsync();
                var ftpItems = await client.GetListing(path);
                var result = new List<FtpItemDto>();

                foreach (var item in ftpItems)
                {
                    result.Add(new FtpItemDto
                    {
                        Name = item.Name,
                        FullName = item.FullName,
                        IsFolder = item.Type == FtpObjectType.Directory,
                        Size = item.Size,
                        Modified = item.Modified
                    });
                }

                _customLogger.LogInfo("SUNUCU_KIMLIK", $"Dizin listelendi: \"{path}\" (Öğe sayısı: {result.Count})");
                return result;
            }
            catch (Exception ex)
            {
                _customLogger.LogError("SUNUCU_KIMLIK", $"Dizin listelenirken hata oluştu: \"{path}\"", ex);
                throw;
            }
        }

        public async Task<bool> UploadFileAsync(Stream fileStream, string remotePath)
        {
            try
            {
                using var client = await CreateClientAsync();
                var status = await client.UploadStream(fileStream, remotePath, FtpRemoteExists.Overwrite);
                
                if (status == FtpStatus.Success)
                {
                    _customLogger.LogInfo("DOSYA_YUKLE", $"Dosya FTP'ye başarıyla yüklendi: \"{remotePath}\"");
                    return true;
                }
                else
                {
                    _customLogger.LogWarning("DOSYA_YUKLE", $"Dosya FTP'ye yüklenirken başarısız durum döndü: \"{remotePath}\" ({status})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_YUKLE", $"Dosya yüklenirken hata oluştu: \"{remotePath}\"", ex);
                throw;
            }
        }

        public async Task<bool> RenameAsync(string sourcePath, string targetPath)
        {
            try
            {
                using var client = await CreateClientAsync();
                await client.Rename(sourcePath, targetPath);
                _customLogger.LogInfo("DOSYA_TASI", $"Öğe taşındı/yeniden adlandırıldı: \"{sourcePath}\" -> \"{targetPath}\"");
                return true;
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_TASI", $"Öğe taşıma hatası (\"{sourcePath}\" -> \"{targetPath}\"): {ex.Message}", ex);
                throw;
            }
        }

        public async Task<MemoryStream> DownloadFileAsync(string remotePath)
        {
            try
            {
                using var client = await CreateClientAsync();
                var memoryStream = new MemoryStream();
                await client.DownloadStream(memoryStream, remotePath);
                memoryStream.Position = 0;
                
                _customLogger.LogInfo("DOSYA_INDIR", $"Dosya FTP'den başarıyla indirildi: \"{remotePath}\"");
                return memoryStream;
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_INDIR", $"Dosya indirilirken hata oluştu: \"{remotePath}\"", ex);
                throw;
            }
        }

        public async Task DeleteItemAsync(string path, bool isFolder)
        {
            try
            {
                using var client = await CreateClientAsync();
                if (isFolder)
                {
                    await client.DeleteDirectory(path, FtpListOption.Recursive);
                    _customLogger.LogInfo("DOSYA_SIL", $"Klasör silindi (Recursive): \"{path}\"");
                }
                else
                {
                    await client.DeleteFile(path);
                    _customLogger.LogInfo("DOSYA_SIL", $"Dosya silindi: \"{path}\"");
                }
            }
            catch (Exception ex)
            {
                _customLogger.LogError("DOSYA_SIL", $"Silme işlemi sırasında hata oluştu: \"{path}\" (Klasör mü: {isFolder})", ex);
                throw;
            }
        }

        public async Task CreateDirectoryAsync(string path)
        {
            try
            {
                using var client = await CreateClientAsync();
                await client.CreateDirectory(path);
                _customLogger.LogInfo("KLASOR_OLUSTUR", $"Klasör oluşturuldu: \"{path}\"");
            }
            catch (Exception ex)
            {
                _customLogger.LogError("KLASOR_OLUSTUR", $"Klasör oluşturulurken hata oluştu: \"{path}\"", ex);
                throw;
            }
        }

        public async Task<bool> VerifyCredentialsAsync()
        {
            try
            {
                using var client = await CreateClientAsync();
                await client.Connect();
                await client.Disconnect();
                return true;
            }
            catch (Exception ex)
            {
                _customLogger.LogError("FTP_BAĞLANTI_HATASI", $"Giriş doğrulama hatası: {ex.Message}");
                return false;
            }
        }
    }
}
