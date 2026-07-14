using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FtpManager.Api.Models;
using Microsoft.Extensions.Logging;

namespace FtpManager.Api.Services
{
    public sealed class NgrokTunnelService : IDisposable
    {
        private readonly ILogger<NgrokTunnelService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(1) };
        private Process? _process;
        private string _lastError = string.Empty;

        public NgrokTunnelService(ILogger<NgrokTunnelService> logger)
        {
            _logger = logger;
        }

        public async Task<SftpTunnelStatus> GetStatusAsync(int localPort, CancellationToken cancellationToken = default)
        {
            var discovered = await DiscoverTunnelAsync(localPort, cancellationToken);
            if (discovered != null)
            {
                discovered.IsOwnedByApplication = _process is { HasExited: false };
                return discovered;
            }

            if (_process is { HasExited: true })
            {
                _process.Dispose();
                _process = null;
            }

            return new SftpTunnelStatus
            {
                LocalPort = localPort,
                Status = string.IsNullOrWhiteSpace(_lastError) ? "Ngrok tuneli kapali." : _lastError
            };
        }

        public async Task<SftpTunnelStatus> StartAsync(int localPort, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var existing = await DiscoverTunnelAsync(localPort, cancellationToken);
                if (existing != null)
                {
                    existing.IsOwnedByApplication = _process is { HasExited: false };
                    existing.Status = "Ngrok tuneli zaten acik.";
                    return existing;
                }

                await EnsureLocalPortIsListeningAsync(localPort, cancellationToken);
                StopOwnedProcess();
                _lastError = string.Empty;

                var authToken = Environment.GetEnvironmentVariable("NGROK_AUTHTOKEN");
                if (string.IsNullOrWhiteSpace(authToken))
                {
                    throw new InvalidOperationException("Ngrok authtoken bulunamadi. Baslatmadan once NGROK_AUTHTOKEN ortam degiskenini ayarlayin.");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "ngrok",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                startInfo.ArgumentList.Add("tcp");
                startInfo.ArgumentList.Add($"--authtoken={authToken}");
                startInfo.ArgumentList.Add($"127.0.0.1:{localPort}");
                startInfo.ArgumentList.Add("--log");
                startInfo.ArgumentList.Add("stdout");
                startInfo.ArgumentList.Add("--log-format");
                startInfo.ArgumentList.Add("json");

                _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _process.OutputDataReceived += CaptureNgrokOutput;
                _process.ErrorDataReceived += CaptureNgrokOutput;

                try
                {
                    if (!_process.Start())
                    {
                        throw new InvalidOperationException("Ngrok baslatilamadi.");
                    }
                }
                catch (Exception ex)
                {
                    StopOwnedProcess();
                    throw new InvalidOperationException("Ngrok bulunamadi veya baslatilamadi. Once ngrok kurulumu ve authtoken ayarini tamamlayin.", ex);
                }

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                for (var attempt = 0; attempt < 24; attempt++)
                {
                    await Task.Delay(250, cancellationToken);
                    var status = await DiscoverTunnelAsync(localPort, cancellationToken);
                    if (status != null)
                    {
                        status.IsOwnedByApplication = true;
                        status.Status = "Ngrok tuneli acik.";
                        _logger.LogInformation("Ngrok SFTP tunnel started: {PublicUrl} -> 127.0.0.1:{LocalPort}", status.PublicUrl, localPort);
                        return status;
                    }

                    if (_process.HasExited)
                    {
                        break;
                    }
                }

                var error = string.IsNullOrWhiteSpace(_lastError)
                    ? "Ngrok tuneli acilamadi. Ngrok authtoken ayarini ve internet baglantisini kontrol edin."
                    : _lastError;
                StopOwnedProcess();
                throw new InvalidOperationException(error);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<SftpTunnelStatus> StopAsync(int localPort, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_process is not { HasExited: false })
                {
                    var external = await DiscoverTunnelAsync(localPort, cancellationToken);
                    if (external != null)
                    {
                        external.Status = "Bu tunel uygulama disindan baslatilmis; onu baslatan terminalden durdurun.";
                        return external;
                    }
                }

                StopOwnedProcess();
                _lastError = string.Empty;
                return new SftpTunnelStatus { LocalPort = localPort, Status = "Ngrok tuneli durduruldu." };
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<SftpTunnelStatus?> DiscoverTunnelAsync(int localPort, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync("http://127.0.0.1:4040/api/tunnels", cancellationToken);
                if (!response.IsSuccessStatusCode) return null;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                foreach (var tunnel in document.RootElement.GetProperty("tunnels").EnumerateArray())
                {
                    if (!tunnel.TryGetProperty("proto", out var proto) || proto.GetString() != "tcp") continue;
                    if (!tunnel.TryGetProperty("config", out var config) || !config.TryGetProperty("addr", out var addr)) continue;
                    var target = addr.GetString() ?? string.Empty;
                    if (!target.EndsWith($":{localPort}", StringComparison.OrdinalIgnoreCase)) continue;

                    var publicUrl = tunnel.GetProperty("public_url").GetString();
                    if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri)) continue;
                    return new SftpTunnelStatus
                    {
                        IsRunning = true,
                        LocalPort = localPort,
                        PublicUrl = publicUrl,
                        PublicHost = uri.Host,
                        PublicPort = uri.Port,
                        Status = "Ngrok tuneli acik."
                    };
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return null;
            }

            return null;
        }

        private static async Task EnsureLocalPortIsListeningAsync(int localPort, CancellationToken cancellationToken)
        {
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync("127.0.0.1", localPort, cancellationToken);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                throw new InvalidOperationException($"Yerel SFTP portu {localPort} dinlenmiyor. OpenSSH servisini ve sshd_config Port ayarini kontrol edin.", ex);
            }
        }

        private void CaptureNgrokOutput(object sender, DataReceivedEventArgs eventArgs)
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Data)) return;
            try
            {
                using var document = JsonDocument.Parse(eventArgs.Data);
                if (document.RootElement.TryGetProperty("lvl", out var level) &&
                    (level.GetString() == "eror" || level.GetString() == "crit"))
                {
                    _lastError = document.RootElement.TryGetProperty("err", out var error) &&
                        !string.IsNullOrWhiteSpace(error.GetString())
                        ? error.GetString()!
                        : document.RootElement.TryGetProperty("msg", out var message)
                            ? message.GetString() ?? eventArgs.Data
                            : eventArgs.Data;
                }
            }
            catch (JsonException)
            {
                if (eventArgs.Data.Contains("error", StringComparison.OrdinalIgnoreCase)) _lastError = eventArgs.Data;
            }
        }

        private void StopOwnedProcess()
        {
            if (_process == null) return;
            try
            {
                if (!_process.HasExited) _process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }

        public void Dispose()
        {
            StopOwnedProcess();
            _httpClient.Dispose();
            _gate.Dispose();
        }
    }
}
