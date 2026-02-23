namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// 1 台のディスクデバイスの累積 I/O 統計スナップショット。
/// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
/// <para>
/// Cumulative I/O statistics snapshot for a single disk device.
/// Values are cumulative since kernel boot. The caller is responsible for computing deltas.
/// </para>
/// </summary>
public readonly record struct DiskDeviceStat(
    /// <summary>BSD デバイス名。例: "disk0"<br/>BSD device name. Example: "disk0"</summary>
    string Name,

    /// <summary>累積読み取りバイト数<br/>Cumulative bytes read</summary>
    ulong BytesRead,

    /// <summary>累積書き込みバイト数<br/>Cumulative bytes written</summary>
    ulong BytesWritten,

    /// <summary>累積読み取り操作数<br/>Cumulative read operations completed</summary>
    ulong ReadsCompleted,

    /// <summary>累積書き込み操作数<br/>Cumulative write operations completed</summary>
    ulong WritesCompleted
);

/// <summary>
/// 全物理ディスクの累積 I/O 統計を管理するクラス。
/// IOKit の IOMedia (Whole) サービスから BSD 名を取得し、
/// 親の IOBlockStorageDriver の Statistics ディクショナリを読み取る。
/// <see cref="Create()"/> でインスタンスを生成し、<see cref="Update()"/> を呼ぶたびに
/// 最新の累積値を更新する。<see cref="NetworkStats"/> と同じパターン。
/// <para>
/// Manages cumulative I/O statistics for all physical disks.
/// Retrieves BSD device names from IOMedia (Whole) services and reads Statistics from
/// the parent IOBlockStorageDriver. Create via <see cref="Create()"/>; call <see cref="Update()"/> to refresh.
/// Values are cumulative since boot; the caller is responsible for computing deltas.
/// </para>
/// </summary>
public sealed class DiskStats
{
    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>
    /// 全物理ディスクの統計エントリ一覧 (名前昇順)。
    /// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
    /// <para>List of statistics entries for all physical disks (sorted by name). Values are cumulative since boot.</para>
    /// </summary>
    public IReadOnlyList<DiskDeviceStat> Devices { get; private set; } = [];

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private DiskStats()
    {
        Update();
    }

    /// <summary>DiskStats インスタンスを生成する。<br/>Creates a DiskStats instance.</summary>
    public static DiskStats Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOKit から全物理ディスクの I/O 統計を取得して Devices を更新する。
    /// 成功時は true、IOKit 呼び出し失敗時は false を返す。
    /// <para>
    /// Fetches I/O statistics for all physical disks from IOKit and updates Devices.
    /// Returns true on success, false if the IOKit call fails.
    /// </para>
    /// </summary>
    public bool Update()
    {
        // IOMedia (Whole=true) を列挙して物理ディスクを取得する
        var iter = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), ref iter);
        if (kr != KERN_SUCCESS || iter == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var results = new List<DiskDeviceStat>();
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    var stat = ReadWholeDiskStat(entry);
                    if (stat.HasValue)
                    {
                        results.Add(stat.Value);
                    }
                }
                finally
                {
                    IOObjectRelease(entry);
                }
            }

            results.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            Devices = results;
            UpdateAt = DateTime.Now;
            return true;
        }
        finally
        {
            IOObjectRelease(iter);
        }
    }

    //--------------------------------------------------------------------------------
    // Private helpers
    //--------------------------------------------------------------------------------

    private static DiskDeviceStat? ReadWholeDiskStat(uint mediaEntry)
    {
        // Whole=true のエントリのみ対象 (物理ディスク全体を表す IOMedia)
        var wholeKey = CFStringCreateWithCString(IntPtr.Zero, "Whole", kCFStringEncodingUTF8);
        if (wholeKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var wholeProp = IORegistryEntryCreateCFProperty(mediaEntry, wholeKey, IntPtr.Zero, 0);
            if (wholeProp == IntPtr.Zero)
            {
                return null;
            }

            var isWhole = CFBooleanGetValue(wholeProp);
            CFRelease(wholeProp);
            if (!isWhole)
            {
                return null;
            }
        }
        finally
        {
            CFRelease(wholeKey);
        }

        // BSD 名を取得 (例: "disk0")
        var bsdName = GetIokitString(mediaEntry, "BSD Name");
        if (bsdName is null)
        {
            return null;
        }

        // 親エントリ (IOBlockStorageDriver) の Statistics を取得
        if (IORegistryEntryGetParentEntry(mediaEntry, "IOService", out var parent) != KERN_SUCCESS || parent == 0)
        {
            return null;
        }

        try
        {
            var statsDict = GetIokitDictionary(parent, "Statistics");
            if (statsDict == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var bytesRead = (ulong)GetIokitDictNumber(statsDict, "Bytes (Read)");
                var bytesWritten = (ulong)GetIokitDictNumber(statsDict, "Bytes (Write)");
                var readsCompleted = (ulong)GetIokitDictNumber(statsDict, "Operations (Read)");
                var writesCompleted = (ulong)GetIokitDictNumber(statsDict, "Operations (Write)");
                return new DiskDeviceStat(bsdName, bytesRead, bytesWritten, readsCompleted, writesCompleted);
            }
            finally
            {
                CFRelease(statsDict);
            }
        }
        finally
        {
            IOObjectRelease(parent);
        }
    }
}
