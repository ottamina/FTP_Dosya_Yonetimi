using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FtpManager.Api.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace FtpManager.Api.Services
{
    public sealed class OpenSshSftpProvisioner
    {
        private readonly ILogger<OpenSshSftpProvisioner> _logger;

        public OpenSshSftpProvisioner(ILogger<OpenSshSftpProvisioner> logger)
        {
            _logger = logger;
        }

        public void Provision(FtpServerConfig server, string chrootDirectory, string dataDirectory, bool rotatePassword = true)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Bu SFTP hazirlama akisi Windows OpenSSH icindir.");
            }

            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                throw new InvalidOperationException("SFTP hesabi ve OpenSSH ayarlari icin API'yi Yonetici olarak baslatin.");
            }

            var sshdConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ssh", "sshd_config");
            if (!File.Exists(sshdConfigPath))
            {
                throw new InvalidOperationException("OpenSSH Server yapilandirmasi bulunamadi. Windows OpenSSH Server ozelligini kurun.");
            }

            var accountSuffix = server.Id.Replace("-", string.Empty, StringComparison.Ordinal);
            if (accountSuffix.Length > 12) accountSuffix = accountSuffix[..12];
            var username = server.SftpUsername ?? $"sftp_{accountSuffix}";
            var password = rotatePassword || string.IsNullOrWhiteSpace(server.SftpPassword)
                ? GeneratePassword()
                : server.SftpPassword;

            WindowsLocalUserManager.CreateOrUpdate(username, password, dataDirectory);
            ConfigureDirectoryPermissions(username, chrootDirectory, dataDirectory);

            var originalConfig = File.ReadAllText(sshdConfigPath);
            var port = ReadSshdPort(originalConfig);
            var updatedConfig = UpsertManagedMatchBlock(originalConfig, username, chrootDirectory);
            var backupPath = $"{sshdConfigPath}.ftp-manager.bak";
            File.Copy(sshdConfigPath, backupPath, true);

            try
            {
                File.WriteAllText(sshdConfigPath, updatedConfig, new UTF8Encoding(false));
                ValidateSshdConfiguration(sshdConfigPath);
                RestartSshdService();
            }
            catch
            {
                File.Copy(backupPath, sshdConfigPath, true);
                try { RestartSshdService(); } catch { }
                throw;
            }

            ValidateSftpSession(username, password, port);

            server.SftpUsername = username;
            server.SftpPassword = password;
            server.SftpEnabled = true;
            server.SftpLocalPort = port;
            server.SftpStatus = $"Hazir ve klasore kisitli (OpenSSH, yerel port {port})";
            _logger.LogInformation("Restricted SFTP account {Username} provisioned for server {ServerId}", username, server.Id);
        }

        public void Deprovision(FtpServerConfig server)
        {
            if (!server.SftpEnabled || string.IsNullOrWhiteSpace(server.SftpUsername)) return;

            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                throw new InvalidOperationException("SFTP hesabini guvenli bicimde kaldirmak icin API'yi Yonetici olarak baslatin.");
            }

            var sshdConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ssh", "sshd_config");
            if (File.Exists(sshdConfigPath))
            {
                var originalConfig = File.ReadAllText(sshdConfigPath);
                var updatedConfig = RemoveManagedMatchBlock(originalConfig, server.SftpUsername);
                if (!string.Equals(originalConfig, updatedConfig, StringComparison.Ordinal))
                {
                    try
                    {
                        File.WriteAllText(sshdConfigPath, updatedConfig, new UTF8Encoding(false));
                        ValidateSshdConfiguration(sshdConfigPath);
                        RestartSshdService();
                    }
                    catch
                    {
                        File.WriteAllText(sshdConfigPath, originalConfig, new UTF8Encoding(false));
                        try { RestartSshdService(); } catch { }
                        throw;
                    }
                }
            }

            WindowsLocalUserManager.DeleteIfExists(server.SftpUsername);
            _logger.LogInformation("SFTP account {Username} removed for server {ServerId}", server.SftpUsername, server.Id);
        }

        private static void ConfigureDirectoryPermissions(string username, string chrootDirectory, string dataDirectory)
        {
            Directory.CreateDirectory(chrootDirectory);
            Directory.CreateDirectory(dataDirectory);

            var baseDirectory = Directory.GetParent(chrootDirectory)?.FullName
                ?? throw new InvalidOperationException("SFTP depolama kok dizini belirlenemedi.");
            var productDirectory = Directory.GetParent(baseDirectory)?.FullName
                ?? throw new InvalidOperationException("SFTP uygulama veri dizini belirlenemedi.");

            var administratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var userSid = (SecurityIdentifier)new NTAccount(Environment.MachineName, username)
                .Translate(typeof(SecurityIdentifier));

            // Chroot ancestors must not be writable by the SFTP account, but the
            // account still needs read/traverse permission to enter its jail.
            SetDirectoryAcl(productDirectory, administratorsSid, systemSid, usersSid, null);
            SetDirectoryAcl(baseDirectory, administratorsSid, systemSid, usersSid, null);
            SetDirectoryAcl(chrootDirectory, administratorsSid, systemSid, userSid, null);
            SetDirectoryAcl(dataDirectory, administratorsSid, systemSid, null, userSid);
        }

        private static void SetDirectoryAcl(
            string path,
            SecurityIdentifier administratorsSid,
            SecurityIdentifier systemSid,
            SecurityIdentifier? readOnlySid,
            SecurityIdentifier? writableUserSid)
        {
            const InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            var security = new DirectorySecurity();
            security.SetOwner(administratorsSid);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                systemSid, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                administratorsSid, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            if (readOnlySid is not null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    readOnlySid,
                    FileSystemRights.ReadAndExecute,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Allow));
            }
            if (writableUserSid is not null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    writableUserSid, FileSystemRights.Modify, inheritance, PropagationFlags.None, AccessControlType.Allow));
            }
            new DirectoryInfo(path).SetAccessControl(security);
        }

        private static void ValidateSftpSession(string username, string password, int port)
        {
            Exception? lastError = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var client = new SftpClient("127.0.0.1", port, username, password);
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                    client.OperationTimeout = TimeSpan.FromSeconds(10);
                    client.Connect();
                    using var entries = client.ListDirectory(".").GetEnumerator();
                    entries.MoveNext();
                    client.Disconnect();
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < 4) System.Threading.Thread.Sleep(500);
                }
            }

            throw new InvalidOperationException(
                $"SFTP kimlik dogrulamasi basarili olsa da klasor oturumu acilamadi: {lastError?.Message}",
                lastError);
        }

        private static string UpsertManagedMatchBlock(string currentConfig, string username, string chrootDirectory)
        {
            var beginMarker = $"# BEGIN FTP MANAGER SFTP {username}";
            var endMarker = $"# END FTP MANAGER SFTP {username}";
            var escapedPath = chrootDirectory.Replace('\\', '/');
            var block = string.Join(Environment.NewLine, new[]
            {
                beginMarker,
                $"Match User {username}",
                $"    ChrootDirectory \"{escapedPath}\"",
                $"    ForceCommand internal-sftp -d /{ServerStorage.DataDirectoryName}",
                "    PermitTTY no",
                "    AllowTcpForwarding no",
                "    X11Forwarding no",
                endMarker
            });

            var escapedBegin = Regex.Escape(beginMarker);
            var escapedEnd = Regex.Escape(endMarker);
            var pattern = $"(?ms)^{escapedBegin}.*?^{escapedEnd}\\s*";
            var withoutExistingBlock = Regex.Replace(currentConfig, pattern, string.Empty).TrimEnd();
            return $"{withoutExistingBlock}{Environment.NewLine}{Environment.NewLine}{block}{Environment.NewLine}";
        }

        private static string RemoveManagedMatchBlock(string currentConfig, string username)
        {
            var beginMarker = Regex.Escape($"# BEGIN FTP MANAGER SFTP {username}");
            var endMarker = Regex.Escape($"# END FTP MANAGER SFTP {username}");
            return Regex.Replace(currentConfig, $"(?ms)^{beginMarker}.*?^{endMarker}\\s*", string.Empty).TrimEnd() + Environment.NewLine;
        }

        private static int ReadSshdPort(string config)
        {
            var match = Regex.Match(config, @"(?im)^\s*Port\s+(\d+)\s*$");
            return match.Success && int.TryParse(match.Groups[1].Value, out var port) ? port : 22;
        }

        private static void ValidateSshdConfiguration(string configPath)
        {
            var sshdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "sshd.exe");
            if (!File.Exists(sshdPath))
            {
                throw new InvalidOperationException("sshd.exe bulunamadi.");
            }
            Run(sshdPath, "-t", "-f", configPath);
        }

        private static void RestartSshdService()
        {
            Run("powershell.exe", "-NoProfile", "-NonInteractive", "-Command", "Restart-Service -Name sshd -Force -ErrorAction Stop");
        }

        private static string GeneratePassword()
        {
            return $"Sftp!{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}aA1";
        }

        private static void Run(string fileName, params string[] arguments)
        {
            var result = RunProcess(fileName, arguments);
            if (result.ExitCode != 0)
            {
                var detail = string.Join(Environment.NewLine, result.StandardError, result.StandardOutput).Trim();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"{fileName} islemi basarisiz oldu." : detail);
            }
        }

        private static ProcessResult RunProcess(string fileName, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{fileName} baslatilamadi.");
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(true);
                throw new TimeoutException($"{fileName} islemi zaman asimina ugradi.");
            }
            Task.WaitAll(standardOutputTask, standardErrorTask);
            return new ProcessResult(process.ExitCode, standardOutputTask.Result, standardErrorTask.Result);
        }

        private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
    }
}
