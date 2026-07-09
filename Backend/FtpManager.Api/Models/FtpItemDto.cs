using System;

namespace FtpManager.Api.Models
{
    public class FtpItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
    }
}
