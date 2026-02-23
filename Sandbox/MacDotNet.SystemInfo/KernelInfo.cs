namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class KernelInfo
{
    /// <summary>OS の種別文字列 (kern.ostype)。例: "Darwin"<br/>OS type string (kern.ostype). Example: "Darwin"</summary>
    public string OsType { get; }

    /// <summary>OS のリリースバージョン (kern.osrelease)。例: "25.3.0"<br/>OS release version (kern.osrelease). Example: "25.3.0"</summary>
    public string OsRelease { get; }

    /// <summary>OS のビルドバージョン (kern.osversion)。例: "25D125"<br/>OS build version (kern.osversion). Example: "25D125"</summary>
    public string OsVersion { get; }

    /// <summary>OS の製品バージョン (kern.osproductversion)。例: "15.3"。取得できない場合は null<br/>OS product version (kern.osproductversion). Example: "15.3". Returns null if unavailable.</summary>
    public string? OsProductVersion { get; }

    /// <summary>OS のリビジョン番号 (kern.osrevision)<br/>OS revision number (kern.osrevision)</summary>
    public int OsRevision { get; }

    /// <summary>カーネルのバージョン文字列 (kern.version)。ビルド詳細を含む完全な文字列<br/>Full kernel version string including build details (kern.version)</summary>
    public string KernelVersion { get; }

    /// <summary>システムの UUID (kern.uuid)<br/>System UUID (kern.uuid)</summary>
    public string Uuid { get; }

    /// <summary>同時に実行できるプロセスの最大数 (kern.maxproc)<br/>Maximum number of simultaneously runnable processes (kern.maxproc)</summary>
    public int MaxProc { get; }

    /// <summary>システム全体でオープンできるファイルの最大数 (kern.maxfiles)<br/>System-wide maximum number of open files (kern.maxfiles)</summary>
    public int MaxFiles { get; }

    /// <summary>1 プロセスあたりのオープンファイル最大数 (kern.maxfilesperproc)<br/>Maximum number of open files per process (kern.maxfilesperproc)</summary>
    public int MaxFilesPerProc { get; }

    /// <summary>exec 時のコマンドライン引数の最大バイト数 (kern.argmax)<br/>Maximum bytes of command-line arguments for exec (kern.argmax)</summary>
    public int ArgMax { get; }

    /// <summary>カーネルのセキュアレベル (kern.securelevel)。0=通常、1=セキュア、-1=永続非セキュア<br/>Kernel secure level (kern.securelevel). 0=normal, 1=secure, -1=permanently insecure</summary>
    public int SecureLevel { get; }

    /// <summary>システムの起動日時 (kern.boottime)<br/>System boot time (kern.boottime)</summary>
    public DateTimeOffset BootTime { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private unsafe KernelInfo()
    {
        var bootTime = DateTimeOffset.MinValue;
        NativeMethods.timeval_boot tv;
        var len = (IntPtr)sizeof(NativeMethods.timeval_boot);
        if (NativeMethods.sysctlbyname("kern.boottime", &tv, ref len, IntPtr.Zero, 0) == 0)
        {
            bootTime = DateTimeOffset.FromUnixTimeSeconds(tv.tv_sec);
        }
        BootTime = bootTime;

        OsType = GetSystemControlString("kern.ostype") ?? string.Empty;
        OsRelease = GetSystemControlString("kern.osrelease") ?? string.Empty;
        OsVersion = GetSystemControlString("kern.osversion") ?? string.Empty;
        OsProductVersion = GetSystemControlString("kern.osproductversion");
        OsRevision = GetSystemControlInt32("kern.osrevision");
        KernelVersion = GetSystemControlString("kern.version") ?? string.Empty;
        Uuid = GetSystemControlString("kern.uuid") ?? string.Empty;
        MaxProc = GetSystemControlInt32("kern.maxproc");
        MaxFiles = GetSystemControlInt32("kern.maxfiles");
        MaxFilesPerProc = GetSystemControlInt32("kern.maxfilesperproc");
        ArgMax = GetSystemControlInt32("kern.argmax");
        SecureLevel = GetSystemControlInt32("kern.securelevel");
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    /// <summary>カーネル情報スナップショットを生成する。<br/>Creates a snapshot of kernel information.</summary>
    public static KernelInfo Create() => new();
}
