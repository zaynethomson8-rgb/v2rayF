using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace v2rayF.Services;

public sealed class UpdateCheckService
{
    private const string Repo = "drmikecrypto/v2rayF";
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("v2rayF", AppVersion.Normalize(AppVersion.Current)));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<UpdateOffer?> CheckAsync(string releaseAssetFileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(releaseAssetFileName))
            return null;

        using var response = await Http.GetAsync(
            $"https://api.github.com/repos/{Repo}/releases/latest",
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString();
        if (string.IsNullOrWhiteSpace(tag) || !AppVersion.IsNewerThanCurrent(tag))
            return null;

        if (!root.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (!string.Equals(name, releaseAssetFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var url = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(url))
                return null;

            return new UpdateOffer
            {
                Tag = tag,
                Version = AppVersion.Normalize(tag),
                DownloadUrl = url,
                AssetFileName = name!
            };
        }

        return null;
    }
}
