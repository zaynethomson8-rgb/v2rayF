using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using v2rayF.Services;

namespace v2rayF.Android.Services;

public sealed class AndroidPlatformIntegration : IPlatformIntegration
{
    public bool IsMobile => true;

    public bool CanUseTunMode => true;

    public string TunRequirementMessage => "Grant VPN permission when prompted.";

    public string? LastProxyMethod { get; private set; }

    public string? LastEstablishError { get; private set; }

    internal static void ReportEstablishError(string? message)
    {
        if (AppServices.Platform is AndroidPlatformIntegration platform)
            platform.LastEstablishError = message;
    }

    public async Task<int?> EstablishVpnAsync(CancellationToken cancellationToken = default)
    {
        LastEstablishError = null;
        var activity = MainActivity.Instance;
        if (activity is null)
            throw new InvalidOperationException("Activity not ready.");

        var prepare = VpnService.Prepare(activity);
        if (prepare is not null)
        {
            var tcs = new TaskCompletionSource<bool>();
            MainActivity.VpnPermissionTcs = tcs;
            activity.StartActivityForResult(prepare, MainActivity.VpnRequestCode);
            var granted = await tcs.Task;
            if (!granted)
                return null;
        }

        var context = activity.ApplicationContext ?? activity;
        return await V2rayVpnService.EstablishAsync(context, cancellationToken);
    }

    public Task EnableProxyAsync(CancellationToken cancellationToken = default)
    {
        LastProxyMethod = "Android VPN";
        return Task.CompletedTask;
    }

    public Task DisableProxyAsync(CancellationToken cancellationToken = default)
    {
        var context = Application.Context!;
        V2rayVpnService.Disconnect(context);
        context.StopService(new Intent(context, typeof(V2rayForegroundService)));
        LastProxyMethod = null;
        return Task.CompletedTask;
    }
}
