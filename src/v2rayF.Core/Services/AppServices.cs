using System;
using System.Threading.Tasks;

namespace v2rayF.Services;

public static class AppServices
{
    public static ICoreEnvironment CoreEnvironment { get; set; } = null!;

    public static IPlatformIntegration Platform { get; set; } = null!;

    public static ICoreProcessHost CoreProcessHost { get; set; } = new ManagedCoreProcessHost();

    /// <summary>Called when the Android activity stops — tear down VPN so network is not left hijacked.</summary>
    public static Func<Task>? EmergencyDisconnectAsync { get; set; }
}
