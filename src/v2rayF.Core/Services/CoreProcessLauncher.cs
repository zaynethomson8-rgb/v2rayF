using System;
using System.Diagnostics;
using System.IO;

namespace v2rayF.Services;

internal static class CoreProcessLauncher
{
    public static bool IsAndroid => OperatingSystem.IsAndroid();

    public static ProcessStartInfo CreateStartInfo(string corePath, string configPath, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = corePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        if (!IsAndroid)
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configPath);
        return startInfo;
    }

    public static Process CreateProcess(string corePath, string configPath, string workingDirectory)
    {
        return new Process
        {
            StartInfo = CreateStartInfo(corePath, configPath, workingDirectory),
            EnableRaisingEvents = !IsAndroid
        };
    }

    public static void Start(Process process, Action<string>? onErrorLine = null)
    {
        if (!process.Start())
            throw new InvalidOperationException("Failed to start Xray core.");

        if (IsAndroid || onErrorLine is null)
            return;

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                onErrorLine(e.Data);
        };
        process.BeginErrorReadLine();
    }

    public static void Kill(Process process)
    {
        if (IsAndroid)
            process.Kill();
        else
            process.Kill(entireProcessTree: true);
    }

    public static string FormatAndroidStartFailure()
    {
        if (!IsAndroid)
            return "Xray core exited immediately after start.";

        return "Xray core failed to start. Reinstall the app or check that your device is ARM64.";
    }
}
