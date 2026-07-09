using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using FtpManager.Api.Models;

namespace FtpManager.Api.Services
{
    public class FtpServerInstance
    {
        private readonly FtpServerConfig _config;
        private readonly string _ftpRoot;
        private readonly ILogger _logger;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _runTask;
        private readonly ConcurrentDictionary<string, TcpClient> _clients = new();

        public FtpServerConfig Config => _config;
        public bool IsRunning => _runTask != null && !_runTask.IsCompleted;

        public FtpServerInstance(FtpServerConfig config, string baseFtpRoot, ILogger logger)
        {
            _config = config;
            _logger = logger;
            
            // Define unique folder path for this FTP server instance
            _ftpRoot = Path.Combine(baseFtpRoot, config.Id);
            if (!Directory.Exists(_ftpRoot))
            {
                Directory.CreateDirectory(_ftpRoot);
            }
        }

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _config.Port);
            
            try
            {
                _listener.Start();
                _logger.LogInformation("FTP Server '{Name}' started on port {Port}", _config.Name, _config.Port);
                _runTask = Task.Run(() => ListenAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start FTP Server '{Name}' on port {Port}", _config.Name, _config.Port);
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            _cts?.Cancel();
            _listener?.Stop();

            foreach (var client in _clients.Values)
            {
                try { client.Close(); } catch { }
            }
            _clients.Clear();

            if (_runTask != null)
            {
                try
                {
                    await _runTask;
                }
                catch (Exception) { /* ignore cancellation exceptions */ }
            }

            _logger.LogInformation("FTP Server '{Name}' on port {Port} stopped.", _config.Name, _config.Port);
            _runTask = null;
            _cts?.Dispose();
            _cts = null;
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync(token);
                    var clientId = Guid.NewGuid().ToString();
                    _clients[clientId] = tcpClient;
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleClientAsync(tcpClient, token);
                        }
                        finally
                        {
                            _clients.TryRemove(clientId, out _);
                            tcpClient.Dispose();
                        }
                    }, token);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Error accepting client for FTP server '{Name}'", _config.Name);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            using var client = tcpClient;
            using var stream = client.GetStream();
            var utf8WithoutBom = new UTF8Encoding(false);
            using var reader = new StreamReader(stream, utf8WithoutBom);
            using var writer = new StreamWriter(stream, utf8WithoutBom) { AutoFlush = true };

            await writer.WriteLineAsync($"220 Welcome to local FTP Server '{_config.Name}'");

            string? username = null;
            bool isLoggedIn = false;
            string currentDirectory = "/";
            string? renameFromPath = null;

            TcpListener? passiveListener = null;
            TcpClient? passiveClient = null;

            try
            {
                while (client.Connected && !cancellationToken.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break;

                    int spaceIdx = line.IndexOf(' ');
                    string cmd = (spaceIdx >= 0 ? line.Substring(0, spaceIdx) : line).ToUpperInvariant();
                    string args = spaceIdx >= 0 ? line.Substring(spaceIdx + 1).Trim() : string.Empty;

                    if (cmd == "QUIT")
                    {
                        await writer.WriteLineAsync("221 Goodbye");
                        break;
                    }

                    if (cmd == "USER")
                    {
                        username = args;
                        await writer.WriteLineAsync("331 Username OK, need password");
                        continue;
                    }

                    if (cmd == "PASS")
                    {
                        if (username == null)
                        {
                            await writer.WriteLineAsync("503 Bad sequence of commands");
                            continue;
                        }

                        if ((username == _config.Username && args == _config.Password) || username == "anonymous")
                        {
                            isLoggedIn = true;
                            await writer.WriteLineAsync("230 User logged in");
                        }
                        else
                        {
                            await writer.WriteLineAsync("530 Invalid username or password");
                            break;
                        }
                        continue;
                    }

                    if (!isLoggedIn)
                    {
                        await writer.WriteLineAsync("530 Please log in first");
                        continue;
                    }

                    if (cmd == "SYST")
                    {
                        await writer.WriteLineAsync("215 UNIX Type: L8");
                    }
                    else if (cmd == "FEAT")
                    {
                        await writer.WriteLineAsync("211-Features:");
                        await writer.WriteLineAsync(" UTF8");
                        await writer.WriteLineAsync("211 End");
                    }
                    else if (cmd == "OPTS")
                    {
                        await writer.WriteLineAsync("200 UTF8 enabled");
                    }
                    else if (cmd == "PWD")
                    {
                        await writer.WriteLineAsync($"257 \"{currentDirectory}\" is current directory");
                    }
                    else if (cmd == "TYPE")
                    {
                        await writer.WriteLineAsync("200 Type set to I");
                    }
                    else if (cmd == "PASV")
                    {
                        if (passiveListener != null)
                        {
                            passiveListener.Stop();
                            passiveListener = null;
                        }

                        passiveListener = new TcpListener(IPAddress.Loopback, 0);
                        passiveListener.Start();
                        int passivePort = ((IPEndPoint)passiveListener.LocalEndpoint).Port;

                        byte p1 = (byte)(passivePort / 256);
                        byte p2 = (byte)(passivePort % 256);

                        await writer.WriteLineAsync($"227 Entering Passive Mode (127,0,0,1,{p1},{p2})");
                    }
                    else if (cmd == "PORT")
                    {
                        await writer.WriteLineAsync("502 Active mode not supported, use PASV");
                    }
                    else if (cmd == "CWD")
                    {
                        string targetDir = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetDir.TrimStart('/')));

                        if (Directory.Exists(physicalPath) && physicalPath.StartsWith(_ftpRoot))
                        {
                            currentDirectory = targetDir;
                            await writer.WriteLineAsync("250 CWD successful");
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 Directory not found");
                        }
                    }
                    else if (cmd == "CDUP")
                    {
                        string targetDir = NormalizePath(currentDirectory, "..");
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetDir.TrimStart('/')));

                        if (Directory.Exists(physicalPath) && physicalPath.StartsWith(_ftpRoot))
                        {
                            currentDirectory = targetDir;
                            await writer.WriteLineAsync("250 CWD successful");
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 Directory not found");
                        }
                    }
                    else if (cmd == "MKD")
                    {
                        string targetDir = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.Combine(_ftpRoot, targetDir.TrimStart('/'));

                        try
                        {
                            Directory.CreateDirectory(physicalPath);
                            await writer.WriteLineAsync($"257 \"{targetDir}\" directory created");
                        }
                        catch
                        {
                            await writer.WriteLineAsync("550 Cannot create directory");
                        }
                    }
                    else if (cmd == "RMD")
                    {
                        string targetDir = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetDir.TrimStart('/')));

                        if (Directory.Exists(physicalPath) && physicalPath.StartsWith(_ftpRoot) && physicalPath != _ftpRoot)
                        {
                            try
                            {
                                Directory.Delete(physicalPath, true);
                                await writer.WriteLineAsync("250 Directory deleted");
                            }
                            catch
                            {
                                await writer.WriteLineAsync("550 Cannot delete directory");
                            }
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 Directory not found or access denied");
                        }
                    }
                    else if (cmd == "DELE")
                    {
                        string targetFile = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetFile.TrimStart('/')));

                        if (File.Exists(physicalPath) && physicalPath.StartsWith(_ftpRoot))
                        {
                            try
                            {
                                File.Delete(physicalPath);
                                await writer.WriteLineAsync("250 File deleted");
                            }
                            catch
                            {
                                await writer.WriteLineAsync("550 Cannot delete file");
                            }
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 File not found");
                        }
                    }
                    else if (cmd == "RNFR")
                    {
                        if (string.IsNullOrEmpty(args))
                        {
                            await writer.WriteLineAsync("501 Syntax error in parameters or arguments");
                            continue;
                        }
                        
                        string targetFile = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetFile.TrimStart('/')));

                        if ((File.Exists(physicalPath) || Directory.Exists(physicalPath)) && physicalPath.StartsWith(_ftpRoot))
                        {
                            renameFromPath = physicalPath;
                            await writer.WriteLineAsync("350 Requested file action pending further information");
                        }
                        else
                        {
                            renameFromPath = null;
                            await writer.WriteLineAsync("550 File or directory not found");
                        }
                    }
                    else if (cmd == "RNTO")
                    {
                        if (renameFromPath == null)
                        {
                            await writer.WriteLineAsync("503 Bad sequence of commands");
                            continue;
                        }

                        if (string.IsNullOrEmpty(args))
                        {
                            await writer.WriteLineAsync("501 Syntax error in parameters or arguments");
                            continue;
                        }

                        string targetFile = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetFile.TrimStart('/')));

                        if (physicalPath.StartsWith(_ftpRoot))
                        {
                            try
                            {
                                if (Directory.Exists(renameFromPath))
                                {
                                    Directory.Move(renameFromPath, physicalPath);
                                }
                                else if (File.Exists(renameFromPath))
                                {
                                    var parentDir = Path.GetDirectoryName(physicalPath);
                                    if (parentDir != null && !Directory.Exists(parentDir))
                                    {
                                        Directory.CreateDirectory(parentDir);
                                    }
                                    File.Move(renameFromPath, physicalPath);
                                }
                                else
                                {
                                    await writer.WriteLineAsync("550 Source file or directory no longer exists");
                                    renameFromPath = null;
                                    continue;
                                }

                                renameFromPath = null;
                                await writer.WriteLineAsync("250 Requested file action okay, completed");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error executing RNTO command");
                                await writer.WriteLineAsync("550 Rename failed");
                            }
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 Access denied");
                        }
                    }
                    else if (cmd == "SIZE")
                    {
                        string targetFile = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetFile.TrimStart('/')));

                        if (File.Exists(physicalPath) && physicalPath.StartsWith(_ftpRoot))
                        {
                            var fileInfo = new FileInfo(physicalPath);
                            await writer.WriteLineAsync($"213 {fileInfo.Length}");
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 File not found");
                        }
                    }
                    else if (cmd == "MDTM")
                    {
                        string targetFile = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetFile.TrimStart('/')));

                        if (File.Exists(physicalPath) && physicalPath.StartsWith(_ftpRoot))
                        {
                            var fileInfo = new FileInfo(physicalPath);
                            await writer.WriteLineAsync($"213 {fileInfo.LastWriteTimeUtc:yyyyMMddHHmmss}");
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 File not found");
                        }
                    }
                    else if (cmd == "LIST" || cmd == "NLST" || cmd == "MLSD")
                    {
                        if (passiveListener == null)
                        {
                            await writer.WriteLineAsync("425 Use PASV first");
                            continue;
                        }

                        await writer.WriteLineAsync("150 File status okay; about to open data connection");

                        try
                        {
                            using var cts = new CancellationTokenSource(5000);
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

                            passiveClient = await passiveListener.AcceptTcpClientAsync(linkedCts.Token);
                            using var dataStream = passiveClient.GetStream();
                            using var dataWriter = new StreamWriter(dataStream, utf8WithoutBom);

                            string listPath = currentDirectory;
                            if (!string.IsNullOrEmpty(args) && !args.StartsWith("-"))
                            {
                                listPath = NormalizePath(currentDirectory, args);
                            }
                            string physicalDir = Path.GetFullPath(Path.Combine(_ftpRoot, listPath.TrimStart('/')));
                            if (Directory.Exists(physicalDir) && physicalDir.StartsWith(_ftpRoot))
                            {
                                if (cmd == "MLSD")
                                {
                                    foreach (var dir in Directory.GetDirectories(physicalDir))
                                    {
                                        var di = new DirectoryInfo(dir);
                                        if (di.Name.StartsWith(".")) continue;
                                        await dataWriter.WriteAsync($"type=dir;modify={di.LastWriteTimeUtc:yyyyMMddHHmmss}; {di.Name}\r\n");
                                    }
                                    foreach (var file in Directory.GetFiles(physicalDir))
                                    {
                                        var fi = new FileInfo(file);
                                        await dataWriter.WriteAsync($"type=file;size={fi.Length};modify={fi.LastWriteTimeUtc:yyyyMMddHHmmss}; {fi.Name}\r\n");
                                    }
                                }
                                else
                                {
                                    foreach (var dir in Directory.GetDirectories(physicalDir))
                                    {
                                        var di = new DirectoryInfo(dir);
                                        if (di.Name.StartsWith(".")) continue;
                                        string dateStr = di.LastWriteTimeUtc.ToString("MMM dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                                        await dataWriter.WriteAsync($"drwxr-xr-x 1 ftp ftp 0 {dateStr} {di.Name}\r\n");
                                    }
                                    foreach (var file in Directory.GetFiles(physicalDir))
                                    {
                                        var fi = new FileInfo(file);
                                        string dateStr = fi.LastWriteTimeUtc.ToString("MMM dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                                        await dataWriter.WriteAsync($"-rw-r--r-- 1 ftp ftp {fi.Length} {dateStr} {fi.Name}\r\n");
                                    }
                                }
                                await dataWriter.FlushAsync();
                            }

                            await writer.WriteLineAsync("226 Transfer complete");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in LIST/MLSD command execution");
                            await writer.WriteLineAsync("425 Can't open data connection");
                        }
                        finally
                        {
                            passiveClient?.Dispose();
                            passiveClient = null;
                            passiveListener.Stop();
                            passiveListener = null;
                        }
                    }
                    else if (cmd == "STOR")
                    {
                        if (passiveListener == null)
                        {
                            await writer.WriteLineAsync("425 Use PASV first");
                            continue;
                        }

                        await writer.WriteLineAsync("150 File status okay; about to open data connection");

                        try
                        {
                            using var cts = new CancellationTokenSource(5000);
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

                            passiveClient = await passiveListener.AcceptTcpClientAsync(linkedCts.Token);
                            using var dataStream = passiveClient.GetStream();

                            string targetFile = NormalizePath(currentDirectory, args);
                            string physicalPath = Path.Combine(_ftpRoot, targetFile.TrimStart('/'));

                            var parentDir = Path.GetDirectoryName(physicalPath);
                            if (parentDir != null && !Directory.Exists(parentDir))
                            {
                                Directory.CreateDirectory(parentDir);
                            }

                            using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                            {
                                await dataStream.CopyToAsync(fs, linkedCts.Token);
                            }

                            await writer.WriteLineAsync("226 Transfer complete");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in STOR command execution");
                            await writer.WriteLineAsync("425 Can't open data connection");
                        }
                        finally
                        {
                            passiveClient?.Dispose();
                            passiveClient = null;
                            passiveListener.Stop();
                            passiveListener = null;
                        }
                    }
                    else if (cmd == "RETR")
                    {
                        if (passiveListener == null)
                        {
                            await writer.WriteLineAsync("425 Use PASV first");
                            continue;
                        }

                        string targetFile = NormalizePath(currentDirectory, args);
                        string physicalPath = Path.GetFullPath(Path.Combine(_ftpRoot, targetFile.TrimStart('/')));

                        if (File.Exists(physicalPath) && physicalPath.StartsWith(_ftpRoot))
                        {
                            await writer.WriteLineAsync("150 File status okay; about to open data connection");

                            try
                            {
                                using var cts = new CancellationTokenSource(5000);
                                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

                                passiveClient = await passiveListener.AcceptTcpClientAsync(linkedCts.Token);
                                using var dataStream = passiveClient.GetStream();

                                using (var fs = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                                {
                                    await fs.CopyToAsync(dataStream, linkedCts.Token);
                                }
                                await dataStream.FlushAsync();

                                await writer.WriteLineAsync("226 Transfer complete");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in RETR command execution");
                                await writer.WriteLineAsync("425 Can't open data connection");
                            }
                            finally
                            {
                                passiveClient?.Dispose();
                                passiveClient = null;
                                passiveListener.Stop();
                                passiveListener = null;
                            }
                        }
                        else
                        {
                            await writer.WriteLineAsync("550 File not found");
                        }
                    }
                    else
                    {
                        await writer.WriteLineAsync("502 Command not implemented");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FTP Connection error handling client");
            }
            finally
            {
                passiveClient?.Dispose();
                passiveListener?.Stop();
            }
        }

        private string NormalizePath(string current, string input)
        {
            if (string.IsNullOrEmpty(input)) return current;
            if (input.StartsWith("/")) return input;

            var parts = (current + "/" + input).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var stack = new System.Collections.Generic.Stack<string>();

            foreach (var part in parts)
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (stack.Count > 0) stack.Pop();
                }
                else
                {
                    stack.Push(part);
                }
            }

            var array = stack.ToArray();
            Array.Reverse(array);
            return "/" + string.Join("/", array);
        }
    }
}
