using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace v2rayF.Services;

public static class UpdateDownloadHelper
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(20)
    };

    static UpdateDownloadHelper()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"v2rayF/{AppVersion.Normalize(AppVersion.Current)}");
    }

    public static async Task<string> DownloadAsync(
        string url,
        string destPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        progress?.Report("Downloading update…");
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(destPath);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return destPath;
    }

    public static string ExtractZip(string zipPath, string extractDir, IProgress<string>? progress)
    {
        progress?.Report("Unpacking…");
        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
        return extractDir;
    }
}
