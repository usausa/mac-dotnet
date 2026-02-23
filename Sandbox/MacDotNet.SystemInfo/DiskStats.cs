namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// 1 台のディスクデバイスの累積 I/O 統計。
/// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
/// <para>
/// Cumulative I/O statistics for a single disk device.
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

    // ---- 静的情報 (インスタンス作成時に一度だけ取得) ----

    /// <summary>
    /// ディスクの製品名。例: "APPLE SSD AP0512Z"、"RTL9210B-CG"。
    /// 取得できない場合は null。
    /// <para>Disk product name (e.g. "APPLE SSD AP0512Z"). Null if unavailable.</para>
    /// </summary>
    public string? MediaName { get; }

    /// <summary>
    /// ディスクのベンダー名。例: "Realtek"。空文字列の場合は null として扱われる。
    /// <para>Disk vendor name (e.g. "Realtek"). Null if empty or unavailable.</para>
    /// </summary>
    public string? VendorName { get; }

    /// <summary>
    /// メディアの種別。例: "Solid State"、"Rotational"。
    /// <para>Medium type (e.g. "Solid State", "Rotational"). Null if unavailable.</para>
    /// </summary>
    public string? MediumType { get; }

    /// <summary>
    /// リムーバブルメディアかどうか (IOMedia "Removable" プロパティ)。
    /// <para>Whether the device is removable (IOMedia "Removable" property).</para>
    /// </summary>
    public bool IsRemovable { get; }

    /// <summary>
    /// 物理ブロックストレージデバイスかどうか。
    /// true の場合、IOKit の親が IOBlockStorageDriver である実メディア (NVMe、USB NVMe など)。
    /// false の場合、APFS コンテナ仮想ディスクなど、物理デバイス上に合成された論理ディスク。
    /// <para>
    /// Whether this is a physical block storage device.
    /// True if the IOKit parent is an IOBlockStorageDriver (e.g. NVMe, USB NVMe).
    /// False for synthesized logical disks such as APFS container virtual disks.
    /// </para>
    /// </summary>
    public bool IsPhysicalMedium { get; }

    internal DiskDeviceStat(string name, bool isPhysicalMedium, string? mediaName, string? vendorName, string? mediumType, bool isRemovable)
    {
        Name = name;
        IsPhysicalMedium = isPhysicalMedium;
        MediaName = mediaName;
        VendorName = string.IsNullOrEmpty(vendorName) ? null : vendorName;
        MediumType = mediumType;
        IsRemovable = isRemovable;
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
    private readonly bool _physicalOnly;

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>
    /// 全ディスクの統計エントリ一覧 (名前昇順)。
    /// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
    /// <para>List of statistics entries for all disks (sorted by name). Values are cumulative since boot.</para>
    /// </summary>
    public IReadOnlyList<DiskDeviceStat> Devices => _devices;

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private DiskStats(bool physicalOnly)
    {
        _physicalOnly = physicalOnly;
        Update();
    }

    /// <summary>
    /// DiskStats インスタンスを生成する。
    /// <para>Creates a DiskStats instance.</para>
    /// </summary>
    /// <param name="physicalOnly">
    /// true の場合、IOKit の親が IOBlockStorageDriver である物理メディアのみを対象にする。
    /// APFS コンテナ仮想ディスク (disk3、disk7 等) は除外される。
    /// <para>
    /// When true, only physical media whose IOKit parent is IOBlockStorageDriver are included.
    /// APFS container virtual disks (e.g. disk3, disk7) are excluded.
    /// </para>
    /// </param>
    public static DiskStats Create(bool physicalOnly = false) => new(physicalOnly);

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOKit から全物理ディスクの I/O 統計を取得して Devices を更新する。
    /// 静的情報 (MediaName 等) は新規デバイス登録時にのみ取得し、以降は再取得しない。
    /// 成功時は true、IOKit 呼び出し失敗時は false を返す。
    /// <para>
    /// Fetches I/O statistics for all physical disks from IOKit and updates Devices.
    /// Static info (MediaName etc.) is read only once when a new device is first seen.
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
                    var dynStat = ReadDynamicStats(entry);
                    if (dynStat is null)
                    {
                        continue;
                    }

                    var (name, isPhysicalMedium, bytesRead, bytesWritten, readsCompleted, writesCompleted) = dynStat.Value;

                    // physicalOnly フィルタ
                    if (_physicalOnly && !isPhysicalMedium)
                    {
                        continue;
                    }

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
                        // 新規デバイス: 静的情報を一度だけ取得してコンストラクタに渡す
                        var (isRemovable, mediaName, vendorName, mediumType) = ReadStaticDeviceInfo(entry);
                        device = new DiskDeviceStat(name, isPhysicalMedium, mediaName, vendorName, mediumType, isRemovable);
                        _devices.Add(device);
                        added = true;
                    }

                    device.Live = true;
                    device.BytesRead = bytesRead;
                    device.BytesWritten = bytesWritten;
                    device.ReadsCompleted = readsCompleted;
                    device.WritesCompleted = writesCompleted;
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
    /// IOMedia エントリから動的な I/O 統計を読み取る。
    /// Whole=true でない場合、BSD 名が取得できない場合、Statistics がない場合は null を返す。
    /// IsPhysicalMedium は親クラスが IOBlockStorageDriver かどうかで判定する。
    /// </summary>
    private static (string Name, bool IsPhysicalMedium, ulong BytesRead, ulong BytesWritten, ulong ReadsCompleted, ulong WritesCompleted)? ReadDynamicStats(uint mediaEntry)
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

        // 親エントリの Statistics を取得
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

            // 親クラスが IOBlockStorageDriver であれば物理メディア
            var parentClass = GetIokitClassName(parent);
            var isPhysicalMedium = parentClass == "IOBlockStorageDriver";

            try
            {
                var bytesRead = (ulong)GetIokitDictNumber(statsDict, "Bytes (Read)");
                var bytesWritten = (ulong)GetIokitDictNumber(statsDict, "Bytes (Write)");
                var readsCompleted = (ulong)GetIokitDictNumber(statsDict, "Operations (Read)");
                var writesCompleted = (ulong)GetIokitDictNumber(statsDict, "Operations (Write)");
                return (bsdName, isPhysicalMedium, bytesRead, bytesWritten, readsCompleted, writesCompleted);
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

    /// <summary>
    /// IOMedia エントリから静的なデバイス情報を読み取る。新規デバイス登録時に一度だけ呼ばれる。
    /// </summary>
    private static (bool IsRemovable, string? MediaName, string? VendorName, string? MediumType) ReadStaticDeviceInfo(uint mediaEntry)
    {
        var isRemovable = GetIokitBoolean(mediaEntry, "Removable");
        var (mediaName, vendorName, mediumType) = FindDeviceCharacteristics(mediaEntry);
        return (isRemovable, mediaName, vendorName, mediumType);
    }

    /// <summary>
    /// IOKit エントリの祖先を IOService プレーンで上方向に辿り、
    /// "Device Characteristics" 辞書中の製品名・ベンダー名・メディア種別を返す。
    /// entry 自体は呼び出し元が所有するため release しない。
    /// </summary>
    private static (string? MediaName, string? VendorName, string? MediumType) FindDeviceCharacteristics(uint entry)
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
                string? mediaName, vendorName, mediumType;
                try
                {
                    mediaName = GetIokitDictString(deviceCharacts, "Product Name");
                    vendorName = GetIokitDictString(deviceCharacts, "Vendor Name");
                    mediumType = GetIokitDictString(deviceCharacts, "Medium Type");
                }
                finally
                {
                    CFRelease(deviceCharacts);
                }

                IOObjectRelease(current);
                return (mediaName, vendorName, mediumType);
            }
        }

        if (shouldReleaseCurrent)
        {
            IOObjectRelease(current);
        }

        return (null, null, null);
    }
}
