using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FtpManager.Api.Services
{
    public class LogService : ILogService
    {
        private readonly string _logsDir;
        private readonly string _dbDir;
        private readonly string _jsonDir;
        private readonly string _textDir;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AccessService _accessService;

        private static readonly object _lock = new object();

        public LogService(IHttpContextAccessor httpContextAccessor, AccessService accessService)
        {
            _httpContextAccessor = httpContextAccessor;
            _accessService = accessService;
            // Set up folders relative to application root
            _logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            _dbDir = Path.Combine(_logsDir, "database");
            _jsonDir = Path.Combine(_logsDir, "json");
            _textDir = Path.Combine(_logsDir, "log");

            // Ensure folders exist
            Directory.CreateDirectory(_dbDir);
            Directory.CreateDirectory(_jsonDir);
            Directory.CreateDirectory(_textDir);
        }

        private string GetDbPath(DateTime date) => Path.Combine(_dbDir, $"app-{date:yyyy-MM-dd}.db");
        private string GetJsonPath(DateTime date) => Path.Combine(_jsonDir, $"app-{date:yyyy-MM-dd}.jsonl");
        private string GetTextPath(DateTime date) => Path.Combine(_textDir, $"app-{date:yyyy-MM-dd}.log");

        private void WriteLog(string operation, string message, string level, string? username = null, string? roleName = null, Exception? exception = null)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                try
                {
                    var context = _httpContextAccessor.HttpContext;
                    if (context != null)
                    {
                        var actor = _accessService.GetCurrentUser(context);
                        username = actor.Username;
                        roleName = actor.RoleName;
                    }
                }
                catch
                {
                    // Background services and failed/anonymous requests have no actor.
                }
            }
            var now = DateTime.Now;
            var entry = new LogEntry
            {
                Timestamp = now.ToString("yyyy-MM-dd HH:mm:ss"),
                Level = level,
                Operation = operation,
                Message = message,
                Username = username,
                RoleName = roleName,
                Exception = exception != null ? $"{exception.Message} | StackTrace: {exception.StackTrace}" : null
            };

            string dbPath = GetDbPath(now);
            string jsonPath = GetJsonPath(now);
            string textPath = GetTextPath(now);

            lock (_lock)
            {
                // 1. Save to daily LiteDB
                try
                {
                    using (var db = new LiteDatabase(dbPath))
                    {
                        var col = db.GetCollection<LogEntry>("logs");
                        col.Insert(entry);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to LiteDB: {ex.Message}");
                }

                // 2. Save to daily JSONL (Append Line)
                try
                {
                    var line = System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine;
                    File.AppendAllText(jsonPath, line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to JSONL log: {ex.Message}");
                }

                // 3. Save to daily Plain Text .log File
                try
                {
                    File.AppendAllText(textPath, $"[{entry.Timestamp}] [{entry.Level}] [{entry.Operation}] [{entry.Username ?? "anonymous"}] [{entry.RoleName ?? "-"}] {entry.Message}{(entry.Exception != null ? $" | Exception: {entry.Exception}" : "")}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to .log file: {ex.Message}");
                }
            }
        }

        public void LogInfo(string operation, string message)
        {
            WriteLog(operation, message, "INFO");
        }

        public void LogInfo(string operation, string message, string? username, string? roleName)
        {
            WriteLog(operation, message, "INFO", username, roleName);
        }

        public void LogWarning(string operation, string message)
        {
            WriteLog(operation, message, "WARN");
        }

        public void LogWarning(string operation, string message, string? username, string? roleName)
        {
            WriteLog(operation, message, "WARN", username, roleName);
        }

        public void LogError(string operation, string message, Exception? ex = null)
        {
            WriteLog(operation, message, "ERROR", null, null, ex);
        }

        public void LogError(string operation, string message, string? username, string? roleName, Exception? ex = null)
        {
            WriteLog(operation, message, "ERROR", username, roleName, ex);
        }

        public Task<List<LogEntry>> GetDatabaseLogsAsync()
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var list = new List<LogEntry>();
                    try
                    {
                        var dbFiles = Directory.GetFiles(_dbDir, "app-*.db");
                        Array.Sort(dbFiles);
                        Array.Reverse(dbFiles); // Sort descending (newest files first)

                        foreach (var file in dbFiles)
                        {
                            using (var db = new LiteDatabase(file))
                            {
                                var col = db.GetCollection<LogEntry>("logs");
                                var items = col.FindAll();
                                var fileList = new List<LogEntry>(items);
                                fileList.Reverse(); // Newest logs first within the day
                                list.AddRange(fileList);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading LiteDB logs: {ex.Message}");
                    }
                    return list;
                }
            });
        }

        public Task<List<LogEntry>> GetJsonLogsAsync()
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var list = new List<LogEntry>();
                    try
                    {
                        var jsonFiles = Directory.GetFiles(_jsonDir, "app-*.jsonl");
                        Array.Sort(jsonFiles);
                        Array.Reverse(jsonFiles); // Sort descending (newest files first)

                        foreach (var file in jsonFiles)
                        {
                            var lines = File.ReadAllLines(file);
                            var fileList = new List<LogEntry>();
                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                try
                                {
                                    var item = System.Text.Json.JsonSerializer.Deserialize<LogEntry>(line);
                                    if (item != null)
                                    {
                                        fileList.Add(item);
                                    }
                                }
                                catch { }
                            }
                            fileList.Reverse(); // Newest logs first within the day
                            list.AddRange(fileList);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading JSONL logs: {ex.Message}");
                    }
                    return list;
                }
            });
        }

        public Task<List<string>> GetTextLogsAsync()
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var list = new List<string>();
                    try
                    {
                        var textFiles = Directory.GetFiles(_textDir, "app-*.log");
                        Array.Sort(textFiles);
                        Array.Reverse(textFiles); // Sort descending (newest files first)

                        foreach (var file in textFiles)
                        {
                            var lines = File.ReadAllLines(file);
                            var fileList = new List<string>(lines);
                            fileList.Reverse(); // Newest logs first within the day
                            list.AddRange(fileList);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading text logs: {ex.Message}");
                    }
                    return list;
                }
            });
        }
    }
}
