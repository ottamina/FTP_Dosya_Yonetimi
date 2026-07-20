using System;
using System.IO;

namespace FtpManager.Api.Services
{
    internal static class ServerStorage
    {
        public const string DataDirectoryName = "data";

        public static string GetSecureBaseRoot()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "FtpManager",
                "ftp_root");
        }

        public static void MigrateLegacyBaseRoot(string legacyBaseRoot, string secureBaseRoot)
        {
            Directory.CreateDirectory(secureBaseRoot);
            if (!Directory.Exists(legacyBaseRoot) ||
                string.Equals(Path.GetFullPath(legacyBaseRoot), Path.GetFullPath(secureBaseRoot), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(legacyBaseRoot))
            {
                var destination = Path.Combine(secureBaseRoot, Path.GetFileName(entry));
                if (Directory.Exists(entry))
                {
                    CopyDirectoryWithoutOverwrite(entry, destination);
                }
                else if (!File.Exists(destination))
                {
                    File.Copy(entry, destination);
                }
            }
        }

        private static void CopyDirectoryWithoutOverwrite(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var destination = Path.Combine(destinationDirectory, relativePath);
                if (!File.Exists(destination))
                {
                    File.Copy(file, destination);
                }
            }
        }

        public static (string ChrootDirectory, string DataDirectory) EnsureLayout(string baseRoot, string serverId)
        {
            var chrootDirectory = Path.GetFullPath(Path.Combine(baseRoot, serverId));
            var dataDirectory = Path.Combine(chrootDirectory, DataDirectoryName);

            Directory.CreateDirectory(chrootDirectory);
            Directory.CreateDirectory(dataDirectory);

            foreach (var entry in Directory.EnumerateFileSystemEntries(chrootDirectory))
            {
                if (string.Equals(entry, dataDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destination = Path.Combine(dataDirectory, Path.GetFileName(entry));
                if (File.Exists(destination) || Directory.Exists(destination))
                {
                    throw new IOException($"Depolama gecisi tamamlanamadi; hedef zaten var: {destination}");
                }

                if (Directory.Exists(entry))
                {
                    Directory.Move(entry, destination);
                }
                else
                {
                    File.Move(entry, destination);
                }
            }

            return (chrootDirectory, dataDirectory);
        }

        public static string ResolveExistingFolder(string baseRoot, string relativeFolder)
        {
            if (string.IsNullOrWhiteSpace(relativeFolder))
            {
                throw new InvalidOperationException("FTP kök klasörü seçilmelidir.");
            }

            var root = Path.GetFullPath(baseRoot);
            var normalizedFolder = relativeFolder.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            // Varsayılan sunucunun kullanıcı verisi teknik olarak default/data altında
            // tutulur. Seçicide "default" görünür; FTP kökü doğrudan veriye bağlanır.
            if (string.Equals(normalizedFolder, "default", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(Path.Combine(root, "default", DataDirectoryName)))
            {
                normalizedFolder = Path.Combine("default", DataDirectoryName);
            }
            var candidate = Path.GetFullPath(Path.Combine(root, normalizedFolder));
            var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(candidate))
            {
                throw new InvalidOperationException("Seçilen FTP kök klasörü geçerli değil.");
            }

            return candidate;
        }
    }
}
