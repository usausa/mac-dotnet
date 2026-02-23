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
public sealed class DiskDeviceStat
{
    internal bool Live { get; set; }

    /// <summary>BSD デバイス名。例: "disk0"<br/>BSD device name. Example: "disk0"</summary>
    public string Name { get; }

    /// <summary>累積読み取りバイト数<br/>Cumulative bytes read</summary>
    public ulong BytesRead { get; internal set; }

    /// <summary>累積書き込みバイト数<br/>Cumulative bytes written</summary>
    public ulong BytesWritten { get; internal set; }

    /// <summary>累積読み取り操作数<br/>Cumulative read operations completed</summary>
    public ulong ReadsCompleted { get; internal set; }

    /// <summary>累積書き込み操作数<br/>Cumulative write operations completed</summary>
    public ulong WritesCompleted { get; internal set; }

    /// <summary>
    /// IOKit から取得したディスクの製品名。例: "APPLE SSD AP0512Q"。
    /// 取得できない場合は null。
    /// <para>Disk product name retrieved from IOKit. Null if unavailable.</para>
    /// </summary>
    public string? MediaName { get; internal set; }

    internal DiskDeviceStat(string name)
    {
        Name = name;
    }
}

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
    private readonly List<DiskDeviceStat> _devices = new();

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>
    /// 全物理ディスクの統計エントリ一覧 (名前昇順)。
    /// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
    /// <para>List of statistics entries for all physical disks (sorted by name). Values are cumulative since boot.</para>
    /// </summary>
    public IReadOnlyList<DiskDeviceStat> Devices => _devices;

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
        foreach (var device in _devices)
        {
            device.Live = false;
        }

        // IOMedia (Whole=true) を列挙して物理ディスクを取得する
        var iter = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), ref iter);
        if (kr != KERN_SUCCESS || iter == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var added = false;
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    var stat = ReadWholeDiskStat(entry);
                    if (stat is null)
                    {
                        continue;
                    }

                    var (name, bytesRead, bytesWritten, readsCompleted, writesCompleted, mediaName) = stat.Value;

                    var device = default(DiskDeviceStat);
                    foreach (var item in _devices)
                    {
                        if (item.Name == name)
                        {
                            device = item;
                            break;
                        }
                    }

                    if (device is null)
                    {
                        device = new DiskDeviceStat(name);
                        _devices.Add(device);
                        added = true;
                    }

                    device.Live = true;
                    device.BytesRead = bytesRead;
                    device.BytesWritten = bytesWritten;
                    device.ReadsCompleted = readsCompleted;
                    device.WritesCompleted = writesCompleted;
                    device.MediaName = mediaName;
                }
                finally
                {
                    IOObjectRelease(entry);
                }
            }

            for (var i = _devices.Count - 1; i >= 0; i--)
            {
                if (!_devices[i].Live)
                {
                    _devices.RemoveAt(i);
                }
            }

            if (added)
            {
                _devices.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            }

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

    /// <summary>
    /// IOKit エントリの祖先を IOService プレーンで上方向に辿り、
    /// "Device Characteristics" 辞書中の "Product Name" を返す。
    /// 見つからない場合は null。entry 自体は呼び出し元が所有するため release しない。
    /// </summary>
    private static string? FindProductName(uint entry)
    {
        var current = entry;
        var shouldReleaseCurrent = false;

        for (var depth = 0; depth < 8; depth++)
        {
            if (IORegistryEntryGetParentEntry(current, "IOService", out var parent) != KERN_SUCCESS || parent == 0)
            {
                break;
            }

            if (shouldReleaseCurrent)
            {
                IOObjectRelease(current);
            }

            current = parent;
            shouldReleaseCurrent = true;

            var deviceCharacts = GetIokitDictionary(current, "Device Characteristics");
            if (deviceCharacts != IntPtr.Zero)
            {
                string? name;
                try
                {
                    name = GetIokitDictString(deviceCharacts, "Product Name");
                }
                finally
                {
                    CFRelease(deviceCharacts);
                }

                IOObjectRelease(current);
                return name;
            }
        }

        if (shouldReleaseCurrent)
        {
            IOObjectRelease(current);
        }

        return null;
    }

    private static (string Name, ulong BytesRead, ulong BytesWritten, ulong ReadsCompleted, ulong WritesCompleted, string? MediaName)? ReadWholeDiskStat(uint mediaEntry)
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

            ulong bytesRead, bytesWritten, readsCompleted, writesCompleted;
            try
            {
                bytesRead = (ulong)GetIokitDictNumber(statsDict, "Bytes (Read)");
                bytesWritten = (ulong)GetIokitDictNumber(statsDict, "Bytes (Write)");
                readsCompleted = (ulong)GetIokitDictNumber(statsDict, "Operations (Read)");
                writesCompleted = (ulong)GetIokitDictNumber(statsDict, "Operations (Write)");
            }
            finally
            {
                CFRelease(statsDict);
            }

            // 製品名は IOKit 祖先を上方向に辿って "Device Characteristics" から取得する。
            // APFS 仮想コンテナ (disk3 等) は複数段上に物理デバイスがあるため、
            // 固定深度ではなく見つかるまで辿る。
            var mediaName = FindProductName(parent);

            return (bsdName, bytesRead, bytesWritten, readsCompleted, writesCompleted, mediaName);
        }
        finally
        {
            IOObjectRelease(parent);
        }
    }
}
