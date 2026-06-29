using System.Reflection;

namespace v2rayF.Services;

public static class AppVersion
{
    public static string Current =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static bool IsNewerThanCurrent(string remoteTagOrVersion)
    {
        var remote = Normalize(remoteTagOrVersion);
        var current = Normalize(Current);
        if (!System.Version.TryParse(remote, out var r) || !System.Version.TryParse(current, out var c))
            return false;
        return r > c;
    }

    public static string Normalize(string value)
    {
        value = value.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
            value = value[1..];
        return value;
    }
}
