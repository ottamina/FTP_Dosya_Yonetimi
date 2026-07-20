using System.Text.Json;
using FtpManager.Api.Models;

namespace FtpManager.Api.Services
{
    public class TrashService
    {
        private readonly string _storageDirectory;
        private readonly object _lock = new();

        public TrashService()
        {
            _storageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs", "trash");
            Directory.CreateDirectory(_storageDirectory);
        }

        public async Task<TrashItem> MoveToTrashAsync(FtpService ftpService, string serverId, string path, bool isFolder)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "/" || path.StartsWith("/.ftp-manager-trash", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bu oge cop kutusuna tasinamaz.");

            var item = new TrashItem
            {
                ServerId = serverId,
                OriginalPath = path,
                IsFolder = isFolder
            };
            item.TrashPath = $"/.ftp-manager-trash/{item.Id}/{Path.GetFileName(path.TrimEnd('/'))}";
            await ftpService.CreateDirectoryAsync(Path.GetDirectoryName(item.TrashPath)?.Replace('\\', '/') ?? "/.ftp-manager-trash");
            await ftpService.RenameAsync(path, item.TrashPath);
            Save(serverId, Get(serverId).Append(item).ToList());
            return item;
        }

        public IReadOnlyList<TrashItem> Get(string serverId) => Read(serverId).OrderByDescending(item => item.DeletedAtUtc).ToList();

        public async Task RestoreAsync(FtpService ftpService, string serverId, string id)
        {
            var items = Get(serverId).ToList();
            var item = items.SingleOrDefault(entry => entry.Id == id) ?? throw new KeyNotFoundException("Cop kutusu kaydi bulunamadi.");
            await ftpService.CreateDirectoryAsync(Path.GetDirectoryName(item.OriginalPath)?.Replace('\\', '/') ?? "/");
            await ftpService.RenameAsync(item.TrashPath, item.OriginalPath);
            items.Remove(item);
            Save(serverId, items);
        }

        private List<TrashItem> Read(string serverId)
        {
            lock (_lock)
            {
                var file = FilePath(serverId);
                if (!File.Exists(file)) return new List<TrashItem>();
                return JsonSerializer.Deserialize<List<TrashItem>>(File.ReadAllText(file)) ?? new List<TrashItem>();
            }
        }

        private void Save(string serverId, List<TrashItem> items)
        {
            lock (_lock)
            {
                File.WriteAllText(FilePath(serverId), JsonSerializer.Serialize(items));
            }
        }

        private string FilePath(string serverId) => Path.Combine(_storageDirectory, $"{string.Concat(serverId.Where(char.IsLetterOrDigit))}.json");
    }
}
