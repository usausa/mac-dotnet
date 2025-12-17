namespace MacDotNet.SystemInfo;

using System.Runtime.Versioning;

#pragma warning disable CA1024
[SupportedOSPlatform("macos")]
public static class PlatformProvider
{
    public static UptimeInfo GetUptime() => new();
}
