using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using v2rayF.Models;

namespace v2rayF.Services;

public sealed class ProxyCoreService : IAsyncDisposable
{
    private readonly ICoreEnvironment _environment;
    private readonly object _stderrLock = new();
    private readonly StringBuilder _recentStderr = new();
    private Process? _process;
    private string? _configPath;
    private bool _manualStop;

    public ProxyCoreService(ICoreEnvironment environment)
    {
        _environment = environment;
    }

    public bool IsRunning => _process is { HasExited: false };

    public ProxyServer? ActiveServer { get; private set; }

    public event EventHandler<bool>? RunningStateChanged;

    public string ResolveCorePath() => _environment.GetCorePath();

    public string ResolveCoresDirectory() => _environment.GetCoresDirectory();

    public bool IsCoreAvailable() => File.Exists(ResolveCorePath());

    public bool HasGeoFiles()
    {
        var cores = ResolveCoresDirectory();
        return File.Exists(Path.Combine(cores, "geoip.dat")) &&
               File.Exists(Path.Combine(cores, "geosite.dat"));
    }

    public async Task StartAsync(ProxyServer server, AppSettings settings, int? tunFd = null, CancellationToken cancellationToken = default)
    {
        await _environment.EnsureCoreAsync(cancellationToken).ConfigureAwait(false);

        if (!IsCoreAvailable())
            throw new FileNotFoundException(
                "Xray core not found.",
                ResolveCorePath());

        if (settings.EnableTunMode && !AppServices.Platform.CanUseTunMode)
            throw new InvalidOperationException(AppServices.Platform.TunRequirementMessage);

        if (settings.RoutingMode == RoutingMode.BypassChina && !HasGeoFiles())
            throw new InvalidOperationException(
                "Bypass China routing requires geoip.dat and geosite.dat in the cores folder.");

        await StopAsync(cancellationToken).ConfigureAwait(false);

        var configJson = XrayConfigBuilder.Build(server, settings, tunFd);
        var configDir = Path.Combine(_environment.GetDataDirectory(), "runtime");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");
        await File.WriteAllTextAsync(_configPath, configJson, cancellationToken).ConfigureAwait(false);

        lock (_stderrLock)
        {
            _recentStderr.Clear();
        }

        var corePath = ResolveCorePath();
        _process = CoreProcessLauncher.CreateProcess(corePath, _configPath, ResolveCoresDirectory());

        if (!CoreProcessLauncher.IsAndroid)
        {
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.Exited += OnProcessExited;
        }

        CoreProcessLauncher.Start(_process, line =>
        {
            lock (_stderrLock)
            {
                if (_recentStderr.Length > 0)
                    _recentStderr.AppendLine();
                _recentStderr.Append(line);
            }
        });

        if (!CoreProcessLauncher.IsAndroid)
            _ = DrainOutputAsync(_process);

        await WaitForCoreReadyAsync(_process, cancellationToken).ConfigureAwait(false);

        if (_process.HasExited)
        {
            var error = GetRecentStderr();
            CleanupProcess(notify: false);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? CoreProcessLauncher.FormatAndroidStartFailure()
                    : FormatStartupError(error));
        }

        ActiveServer = server;
        RunningStateChanged?.Invoke(this, true);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var process = Interlocked.Exchange(ref _process, null);
        if (process is null)
            return;

        _manualStop = true;
        try
        {
            if (!process.HasExited)
            {
                CoreProcessLauncher.Kill(process);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(3000);
                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Timed out waiting for exit.
                }
            }
        }
        catch
        {
            // Best effort shutdown.
        }
        finally
        {
            if (!CoreProcessLauncher.IsAndroid)
            {
                process.ErrorDataReceived -= OnErrorDataReceived;
                process.Exited -= OnProcessExited;
            }
            process.Dispose();
            ActiveServer = null;
            RunningStateChanged?.Invoke(this, false);
            _manualStop = false;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_manualStop)
            return;

        ActiveServer = null;
        RunningStateChanged?.Invoke(this, false);
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data))
            return;

        lock (_stderrLock)
        {
            if (_recentStderr.Length > 0)
                _recentStderr.AppendLine();
            _recentStderr.Append(e.Data);
        }
    }

    private static async Task WaitForCoreReadyAsync(Process process, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 40; i++)
        {
            if (process.HasExited)
                return;

            if (await IsPortOpenAsync("127.0.0.1", XrayConfigBuilder.SocksPort, cancellationToken).ConfigureAwait(false))
                return;

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsPortOpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(200);
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task DrainOutputAsync(Process process)
    {
        try
        {
            while (!process.HasExited)
            {
                var line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                    break;
            }
        }
        catch
        {
            // Process ended.
        }
    }

    private void CleanupProcess(bool notify)
    {
        if (_process is null)
            return;

        _process.ErrorDataReceived -= OnErrorDataReceived;
        _process.Exited -= OnProcessExited;
        _process.Dispose();
        _process = null;
        ActiveServer = null;

        if (notify)
            RunningStateChanged?.Invoke(this, false);
    }

    private string GetRecentStderr()
    {
        lock (_stderrLock)
        {
            return _recentStderr.ToString().Trim();
        }
    }

    private static string FormatStartupError(string stderr)
    {
        if (stderr.Contains("10808", StringComparison.Ordinal) ||
            stderr.Contains("10809", StringComparison.Ordinal) ||
            stderr.Contains("bind:", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase))
        {
            return "Local proxy ports 10808/10809 are already in use. Close v2rayN (or another proxy client) and try again.";
        }

        if (string.IsNullOrWhiteSpace(stderr))
            return "Xray core exited immediately after start.";

        var lastLine = stderr;
        var newline = stderr.LastIndexOf('\n');
        if (newline >= 0 && newline < stderr.Length - 1)
            lastLine = stderr[(newline + 1)..].Trim();

        return lastLine;
    }
}
