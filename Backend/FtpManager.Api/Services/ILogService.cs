using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FtpManager.Api.Services
{
    public class LogEntry
    {
        public int Id { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty; // INFO, WARNING, ERROR
        public string Type => Level;
        public string Operation { get; set; } = string.Empty; // E.g., KULLANICI_GIRIS, DOSYA_YUKLE
        public string Message { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? RoleName { get; set; }
        public string? Exception { get; set; }
    }

    public interface ILogService
    {
        void LogInfo(string operation, string message);
        void LogInfo(string operation, string message, string? username, string? roleName);
        void LogWarning(string operation, string message);
        void LogWarning(string operation, string message, string? username, string? roleName);
        void LogError(string operation, string message, Exception? ex = null);
        void LogError(string operation, string message, string? username, string? roleName, Exception? ex = null);
        Task<List<LogEntry>> GetDatabaseLogsAsync();
        Task<List<LogEntry>> GetJsonLogsAsync();
        Task<List<string>> GetTextLogsAsync();
    }
}
