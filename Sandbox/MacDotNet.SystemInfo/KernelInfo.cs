namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class KernelInfo
{
    public string OsType { get; }

    public string OsRelease { get; }

    public string OsVersion { get; }

    public string? OsProductVersion { get; }

    public int OsRevision { get; }

    public string KernelVersion { get; }

    public string Uuid { get; }

    public int MaxProcesses { get; }

    public int MaxProcessesPerUser { get; }

    public int MaxFiles { get; }

    public int MaxFilesPerProcess { get; }

    public int MaxArguments { get; }

    public int SecureLevel { get; }

    public DateTimeOffset BootTime { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal KernelInfo()
    {
        // ReSharper disable StringLiteralTypo
        OsType = GetSystemControlString("kern.ostype") ?? string.Empty;
        OsRelease = GetSystemControlString("kern.osrelease") ?? string.Empty;
        OsVersion = GetSystemControlString("kern.osversion") ?? string.Empty;
        OsProductVersion = GetSystemControlString("kern.osproductversion");
        OsRevision = GetSystemControlInt32("kern.osrevision");
        KernelVersion = GetSystemControlString("kern.version") ?? string.Empty;
        Uuid = GetSystemControlString("kern.uuid") ?? string.Empty;
        MaxProcesses = GetSystemControlInt32("kern.maxproc");
        MaxProcessesPerUser = GetSystemControlInt32("kern.maxprocperuid");
        MaxFiles = GetSystemControlInt32("kern.maxfiles");
        MaxFilesPerProcess = GetSystemControlInt32("kern.maxfilesperproc");
        MaxArguments = GetSystemControlInt32("kern.argmax");
        SecureLevel = GetSystemControlInt32("kern.securelevel");

        var time = new timeval { tv_sec = 0, tv_usec = 0 };
        var size = Marshal.SizeOf<timeval>();
        BootTime = sysctlbyname("kern.boottime", ref time, ref size, IntPtr.Zero, 0) == 0
            ? DateTimeOffset.FromUnixTimeSeconds(time.tv_sec)
            : DateTimeOffset.MinValue;
        // ReSharper restore StringLiteralTypo
    }
}
