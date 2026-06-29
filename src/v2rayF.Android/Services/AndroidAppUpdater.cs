using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using v2rayF.Services;

namespace v2rayF.Android.Services;

public sealed class AndroidAppUpdater : IAppUpdater
{
    public string ReleaseAssetFileName => "v2rayF-android-arm64.zip";

    public Task ApplyUpdateAsync(UpdateOffer offer, IProgress<string>? progress, CancellationToken cancellationToken = default) =>
        AndroidUiThread.InvokeAsync(() => ApplyOnUiThreadAsync(offer, progress, cancellationToken));

    private static async Task ApplyOnUiThreadAsync(
        UpdateOffer offer,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var ctx = Application.Context ?? throw new InvalidOperationException("Application context missing.");
        var cacheRoot = Path.Combine(ctx.CacheDir!.AbsolutePath, "updates");
        var workDir = Path.Combine(cacheRoot, offer.Version);
        Directory.CreateDirectory(workDir);

        var zipPath = Path.Combine(workDir, offer.AssetFileName);
        await UpdateDownloadHelper.DownloadAsync(offer.DownloadUrl, zipPath, progress, cancellationToken)
            .ConfigureAwait(true);

        var extractDir = Path.Combine(workDir, "files");
        UpdateDownloadHelper.ExtractZip(zipPath, extractDir, progress);

        var apk = Directory.EnumerateFiles(extractDir, "*.apk", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException("Update package did not contain an APK.");

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var pm = ctx.PackageManager!;
            if (!pm.CanRequestPackageInstalls())
            {
                progress?.Report("Allow installs, then tap Update again.");
                var settings = new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources,
                    global::Android.Net.Uri.Parse("package:" + ctx.PackageName));
                settings.AddFlags(ActivityFlags.NewTask);
                ctx.StartActivity(settings);
                throw new InvalidOperationException("Install permission required.");
            }
        }

        progress?.Report("Opening installer…");
        var apkFile = new Java.IO.File(apk);
        var authority = ctx.PackageName + ".fileprovider";
        var uri = FileProvider.GetUriForFile(ctx, authority, apkFile);

        var install = new Intent(Intent.ActionView);
        install.SetDataAndType(uri, "application/vnd.android.package-archive");
        install.AddFlags(ActivityFlags.GrantReadUriPermission);
        install.AddFlags(ActivityFlags.NewTask);

        ctx.StartActivity(install);
    }
}
