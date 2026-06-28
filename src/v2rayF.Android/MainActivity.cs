using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia.Android;
using System.Threading.Tasks;

namespace v2rayF.Android;

[Activity(
    Label = "v2rayF",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    public const int VpnRequestCode = 9001;
    public const int NotificationPermissionRequestCode = 9002;
    public static MainActivity? Instance { get; private set; }
    public static TaskCompletionSource<bool>? VpnPermissionTcs { get; set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);
        RequestNotificationPermissionIfNeeded();
    }

    private void RequestNotificationPermissionIfNeeded()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return;

        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications) == Permission.Granted)
            return;

        ActivityCompat.RequestPermissions(this, [Manifest.Permission.PostNotifications], NotificationPermissionRequestCode);
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == VpnRequestCode)
            VpnPermissionTcs?.TrySetResult(resultCode == Result.Ok);
    }
}
