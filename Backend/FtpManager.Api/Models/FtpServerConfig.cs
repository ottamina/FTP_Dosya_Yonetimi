using System;

namespace FtpManager.Api.Models
{
    public class FtpServerConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = false;
        public bool IsRunning { get; set; } = false; // Run state
        public string? HostWarning { get; set; }
    }
}
