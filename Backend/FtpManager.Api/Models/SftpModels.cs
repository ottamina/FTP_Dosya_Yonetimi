namespace FtpManager.Api.Models
{
    public sealed class SftpTunnelStatus
    {
        public bool IsRunning { get; set; }
        public bool IsOwnedByApplication { get; set; }
        public int LocalPort { get; set; }
        public string? PublicUrl { get; set; }
        public string? PublicHost { get; set; }
        public int? PublicPort { get; set; }
        public string Status { get; set; } = "Ngrok tuneli kapali.";
    }
}
