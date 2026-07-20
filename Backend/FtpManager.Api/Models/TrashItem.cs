namespace FtpManager.Api.Models
{
    public class TrashItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ServerId { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string TrashPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
