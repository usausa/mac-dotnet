namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class KernelInfo
{
    /// <summary>OS の種別文字列 (kern.ostype)。例: "Darwin"</summary>
    public string OsType { get; }

    /// <summary>OS のリリースバージョン (kern.osrelease)。例: "25.3.0"</summary>
    public string OsRelease { get; }

    /// <summary>OS のビルドバージョン (kern.osversion)。例: "25D125"</summary>
    public string OsVersion { get; }

    /// <summary>OS の製品バージョン (kern.osproductversion)。例: "15.3"。取得できない場合は null</summary>
    public string? OsProductVersion { get; }

    /// <summary>OS のリビジョン番号 (kern.osrevision)</summary>
    public int OsRevision { get; }

    /// <summary>カーネルのバージョン文字列 (kern.version)。ビルド詳細を含む完全な文字列</summary>
    public string KernelVersion { get; }

    /// <summary>システムの UUID (kern.uuid)</summary>
    public string Uuid { get; }

    /// <summary>同時に実行できるプロセスの最大数 (kern.maxproc)</summary>
    public int MaxProc { get; }

    /// <summary>システム全体でオープンできるファイルの最大数 (kern.maxfiles)</summary>
    public int MaxFiles { get; }

    /// <summary>1 プロセスあたりのオープンファイル最大数 (kern.maxfilesperproc)</summary>
    public int MaxFilesPerProc { get; }

    /// <summary>exec 時のコマンドライン引数の最大バイト数 (kern.argmax)</summary>
    public int ArgMax { get; }

    /// <summary>カーネルのセキュアレベル (kern.securelevel)。0=通常、1=セキュア、-1=永続非セキュア</summary>
    public int SecureLevel { get; }

    /// <summary>システムの起動日時 (kern.boottime)</summary>
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

    public static KernelInfo Create() => new();
}
