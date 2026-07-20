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
        public string RootFolder { get; set; } = string.Empty;
        public bool TlsEnabled { get; set; }
        public string? CertificatePath { get; set; }
        public string? PrivateKeyPath { get; set; }
        public string? CertificateSubject { get; set; }
        public string? CertificateFingerprint { get; set; }
        public DateTime? CertificateExpiresAtUtc { get; set; }
        public bool CertificateExpiresSoon { get; set; }
        public bool IsActive { get; set; } = false;
        public bool IsRunning { get; set; } = false; // Run state
        public string? HostWarning { get; set; }
        public string? SftpUsername { get; set; }
        public string? SftpPassword { get; set; }
        public bool SftpEnabled { get; set; }
        public string? SftpStatus { get; set; }
        public int SftpLocalPort { get; set; } = 2222;
    }
}
