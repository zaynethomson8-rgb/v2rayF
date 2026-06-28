using System;
using System.Threading;
using System.Threading.Tasks;
using Java.IO;
using Java.Lang;
using v2rayF.Services;
using Process = Java.Lang.Process;

namespace v2rayF.Android.Services;

/// <summary>
/// Starts Xray via Android ProcessBuilder — System.Diagnostics.Process crashes on many devices.
/// </summary>
public sealed class AndroidJavaCoreProcessHost : ICoreProcessHost
{
    private readonly object _lock = new();
    private Process? _process;
    private string _recentOutput = "";

    public bool IsRunning
    {
        get
        {
            lock (_lock)
                return _process is { IsAlive: true };
        }
    }

    public bool HasExited
    {
        get
        {
            lock (_lock)
                return _process is null || !_process.IsAlive;
        }
    }

    public Task StartAsync(
        string corePath,
        string configPath,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        StopAsync(cancellationToken).GetAwaiter().GetResult();

        lock (_lock)
            _recentOutput = "";

        var cmd = new[] { corePath, "run", "-c", configPath };
        var builder = new ProcessBuilder(cmd);
        builder.Directory(new File(workingDirectory));
        builder.RedirectErrorStream(true);

        Process process;
        lock (_lock)
        {
            _process = builder.Start();
            process = _process;
        }

        if (process is not null)
            _ = Task.Run(() => DrainOutputAsync(process), cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_process is null)
                return Task.CompletedTask;

            try
            {
                if (_process.IsAlive)
                    _process.Destroy();
            }
            catch
            {
                // Best effort shutdown.
            }
            finally
            {
                _process = null;
            }
        }

        return Task.CompletedTask;
    }

    public string GetRecentError()
    {
        lock (_lock)
        {
            if (_process is { IsAlive: true })
                return _recentOutput.Trim();

            if (_process is not null)
            {
                try
                {
                    var code = _process.ExitValue();
                    var output = _recentOutput.Trim();
                    return string.IsNullOrEmpty(output)
                        ? $"Xray exited with code {code}"
                        : output;
                }
                catch (IllegalThreadStateException)
                {
                    return _recentOutput.Trim();
                }
            }

            return _recentOutput.Trim();
        }
    }

    private void DrainOutputAsync(Process process)
    {
        try
        {
            using var reader = new BufferedReader(new InputStreamReader(process.InputStream));
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                lock (_lock)
                {
                    if (_recentOutput.Length > 0)
                        _recentOutput += '\n';
                    _recentOutput += line;
                    if (_recentOutput.Length > 8192)
                        _recentOutput = _recentOutput[^4096..];
                }
            }
        }
        catch
        {
            // Process ended.
        }
    }
}
