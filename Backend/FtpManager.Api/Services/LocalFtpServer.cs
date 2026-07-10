using LiteDB;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FtpManager.Api.Models;

namespace FtpManager.Api.Services
{
    public class LocalFtpServer : BackgroundService
    {
        private readonly string _dbFilePath;
        private readonly string _ftpBaseRoot;
        private readonly ILogger<LocalFtpServer> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<string, FtpServerInstance> _instances = new();
        private readonly object _lock = new();

        public LocalFtpServer(ILogger<LocalFtpServer> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            
            // Set up config database path
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            var dbDir = Path.Combine(logsDir, "database");
            Directory.CreateDirectory(dbDir);
            _dbFilePath = Path.Combine(dbDir, "ftp_manager.db");
            
            _ftpBaseRoot = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "ftp_root");
            Directory.CreateDirectory(_ftpBaseRoot);

            // Clean up temporary uploads directory on startup
            var tempRoot = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp");
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
                Directory.CreateDirectory(tempRoot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Uygulama başlangıcında geçici yükleme dizini temizlenirken hata oluştu.");
            }
            
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            lock (_lock)
            {
                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    
                    // Insert default server config if database is empty
                    if (col.Count() == 0)
                    {
                        col.Insert(new FtpServerConfig
                        {
                            Id = "default",
                            Name = "Varsayılan FTP",
                            Host = "127.0.0.1",
                            Port = 2121,
                            Username = "ftpadmin",
                            Password = "admin123",
                            IsActive = true
                        });
                    }
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FTP Manager Background Service starting...");
            
            // Start all active servers from LiteDB
            List<FtpServerConfig> activeConfigs = new();
            lock (_lock)
            {
                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    activeConfigs.AddRange(col.Find(c => c.IsActive));
                }
            }

            foreach (var config in activeConfigs)
            {
                try
                {
                    var instance = new FtpServerInstance(config, _ftpBaseRoot, _loggerFactory.CreateLogger<FtpServerInstance>());
                    instance.Start();
                    _instances[config.Id] = instance;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-start FTP server '{Name}' on port {Port}", config.Name, config.Port);
                }
            }

            // Keep background service alive until stopped
            var tcs = new TaskCompletionSource();
            using (stoppingToken.Register(() => tcs.SetResult()))
            {
                await tcs.Task;
            }

            _logger.LogInformation("FTP Manager Background Service stopping, shutting down all running FTP servers...");
            
            // Stop all instances
            foreach (var instance in _instances.Values)
            {
                await instance.StopAsync();
            }
            _instances.Clear();
        }

        // Public CRUD & Management APIs
        public List<FtpServerConfig> GetServers()
        {
            lock (_lock)
            {
                var list = new List<FtpServerConfig>();
                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    var configs = col.FindAll();
                    foreach (var config in configs)
                    {
                        var hasInstance = _instances.TryGetValue(config.Id, out var instance);
                        list.Add(new FtpServerConfig
                        {
                            Id = config.Id,
                            Name = config.Name,
                            Host = config.Host,
                            Port = config.Port,
                            Username = config.Username,
                            Password = config.Password,
                            IsActive = config.IsActive,
                            IsRunning = hasInstance && instance != null && instance.IsRunning,
                            HostWarning = GetHostWarning(config.Host)
                        });
                    }
                }
                return list;
            }
        }

        public FtpServerConfig? GetServer(string id)
        {
            lock (_lock)
            {
                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    var config = col.FindOne(c => c.Id == id);
                    if (config == null) return null;
                    
                    var hasInstance = _instances.TryGetValue(config.Id, out var instance);
                    return new FtpServerConfig
                    {
                        Id = config.Id,
                        Name = config.Name,
                        Host = config.Host,
                        Port = config.Port,
                        Username = config.Username,
                        Password = config.Password,
                        IsActive = config.IsActive,
                        IsRunning = hasInstance && instance != null && instance.IsRunning,
                        HostWarning = GetHostWarning(config.Host)
                    };
                }
            }
        }

        public FtpServerConfig AddServer(FtpServerConfig newConfig)
        {
            lock (_lock)
            {
                newConfig.Host = (newConfig.Host ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(newConfig.Host))
                {
                    throw new InvalidOperationException("Host/IP boş olamaz.");
                }

                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    ValidateHost(newConfig.Host);
                    
                    // Ensure port is not already used
                    if (col.Exists(c => c.Port == newConfig.Port))
                    {
                        throw new InvalidOperationException($"Port {newConfig.Port} is already in use by another server.");
                    }

                    newConfig.Id = Guid.NewGuid().ToString();
                    newConfig.IsRunning = false;
                    
                    col.Insert(newConfig);
                }

                if (newConfig.IsActive)
                {
                    try
                    {
                        var instance = new FtpServerInstance(newConfig, _ftpBaseRoot, _loggerFactory.CreateLogger<FtpServerInstance>());
                        instance.Start();
                        _instances[newConfig.Id] = instance;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start newly added FTP server '{Name}' on port {Port}", newConfig.Name, newConfig.Port);
                        using (var db = new LiteDatabase(_dbFilePath))
                        {
                            db.GetCollection<FtpServerConfig>("servers").Delete(newConfig.Id);
                        }
                        throw new InvalidOperationException($"FTP sunucusu başlatılamadı: {ex.Message}", ex);
                    }
                }

                return newConfig;
            }
        }

        public void DeleteServer(string id)
        {
            if (id == "default")
            {
                throw new InvalidOperationException("Varsayılan FTP sunucusu silinemez.");
            }

            lock (_lock)
            {
                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    var config = col.FindOne(c => c.Id == id);
                    if (config == null) return;

                    // Stop if running
                    if (_instances.TryRemove(id, out var instance))
                    {
                        Task.Run(() => instance.StopAsync()).Wait();
                    }

                    col.Delete(id);
                }

                // Clean directory
                var dirPath = Path.Combine(_ftpBaseRoot, id);
                if (Directory.Exists(dirPath))
                {
                    try { Directory.Delete(dirPath, true); } catch { }
                }
            }
        }

        public void StartServer(string id)
        {
            lock (_lock)
            {
                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    var config = col.FindOne(c => c.Id == id);
                    if (config == null) throw new KeyNotFoundException("Server not found.");
                    ValidateHost(config.Host);

                    if (_instances.TryGetValue(id, out var existing) && existing.IsRunning)
                    {
                        return; // Already running
                    }

                    var instance = new FtpServerInstance(config, _ftpBaseRoot, _loggerFactory.CreateLogger<FtpServerInstance>());
                    instance.Start();
                    _instances[id] = instance;

                    config.IsActive = true;
                    col.Update(config);
                }
            }
        }

        public async Task StopServerAsync(string id)
        {
            FtpServerInstance? instance = null;
            lock (_lock)
            {
                using (var db = new LiteDatabase(_dbFilePath))
                {
                    var col = db.GetCollection<FtpServerConfig>("servers");
                    var config = col.FindOne(c => c.Id == id);
                    if (config == null) throw new KeyNotFoundException("Server not found.");

                    config.IsActive = false;
                    col.Update(config);
                }

                _instances.TryRemove(id, out instance);
            }

            if (instance != null)
            {
                await instance.StopAsync();
            }
        }

        private static HostValidationResult ValidateHost(string host)
        {
            var addresses = ResolveHostAddresses(host);
            if (addresses.Length == 0)
            {
                throw new InvalidOperationException($"Host çözümlenemedi: {host}.");
            }

            var invalidAddress = Array.Find(addresses, IsInvalidAdvertisedAddress);
            if (invalidAddress != null)
            {
                throw new InvalidOperationException(
                    $"Bu IP FTP hostu olarak kullanılamaz: {invalidAddress}. Bu bilgisayara atanmış gerçek bir IP, 127.0.0.1, LAN IP veya DNS ile bu makineye yönlenen bir domain girin.");
            }

            var localAddresses = GetLocalIPv4Addresses();
            var hasLocalAddress = Array.Exists(addresses, address =>
                Array.Exists(localAddresses, localAddress => localAddress.Equals(address)) || IPAddress.IsLoopback(address));
            var hasPrivateAddress = Array.Exists(addresses, IsPrivateIPv4Address);
            var hasPublicAddress = Array.Exists(addresses, address => address.AddressFamily == AddressFamily.InterNetwork && !IsPrivateIPv4Address(address) && !IPAddress.IsLoopback(address));

            if (hasLocalAddress)
            {
                return new HostValidationResult(null);
            }

            if (hasPrivateAddress)
            {
                throw new InvalidOperationException(
                    $"Host bu bilgisayara ait görünmüyor: {host}. Aynı ağda kullanılacaksa bu makinenin gerçek LAN IP adresini girin. Bu bilgisayardaki IP'ler: {string.Join(", ", localAddresses)}");
            }

            if (hasPublicAddress)
            {
                throw new InvalidOperationException(
                    $"Host public bir adrese gidiyor ve bu local FTP sunucusu icin bu bilgisayara ait gorunmuyor: {host}. Local kullanim icin 127.0.0.1, bu makinenin LAN IP'si veya bu makineye cozumlenen hostname girin.");
            }

            return new HostValidationResult("Host çözüldü, ancak bu bilgisayara ait olduğu doğrulanamadı.");
        }

        private static string? GetHostWarning(string host)
        {
            try
            {
                return ValidateHost(host).Warning;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static IPAddress[] ResolveHostAddresses(string host)
        {
            if (IPAddress.TryParse(host, out var parsedAddress))
            {
                return new[] { parsedAddress };
            }

            try
            {
                return Array.FindAll(Dns.GetHostAddresses(host), address => address.AddressFamily == AddressFamily.InterNetwork);
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException($"Host çözümlenemedi: {host}. DNS kaydını veya hosts dosyasını kontrol edin.", ex);
            }
        }

        private static IPAddress[] GetLocalIPv4Addresses()
        {
            var addresses = new List<IPAddress> { IPAddress.Loopback };
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(unicastAddress.Address);
                    }
                }
            }

            return addresses.ToArray();
        }

        private static bool IsInvalidAdvertisedAddress(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 0 ||
                   bytes[0] >= 224 ||
                   bytes[0] == 169 && bytes[1] == 254 ||
                   address.Equals(IPAddress.Broadcast) ||
                   address.Equals(IPAddress.Any);
        }

        private static bool IsPrivateIPv4Address(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168;
        }

        private sealed record HostValidationResult(string? Warning);
    }
}
