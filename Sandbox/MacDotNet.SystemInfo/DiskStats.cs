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

    /// <summary>
    /// 親エントリ (IOBlockStorageDriver) の IOKit レジストリ ID。
    /// 同一ブート中に同じ Statistics カウンタを追跡するためのキー。
    /// デバイスが抜き差しされると新しいエントリが生成され、ID が変わる。
    /// <para>
    /// IOKit registry ID of the parent entry (IOBlockStorageDriver).
    /// Used as the identity key to track the same Statistics counter within a single boot.
    /// The ID changes when a device is removed and re-inserted.
    /// </para>
    /// </summary>
    internal ulong RegistryEntryId { get; }

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

    /// <summary>読み取り合計時間 (ナノ秒)<br/>Total read time in nanoseconds</summary>
    public ulong TotalTimeRead { get; internal set; }

    /// <summary>書き込み合計時間 (ナノ秒)<br/>Total write time in nanoseconds</summary>
    public ulong TotalTimeWritten { get; internal set; }

    /// <summary>読み取りリトライ回数<br/>Number of read retries</summary>
    public ulong RetriesRead { get; internal set; }

    /// <summary>書き込みリトライ回数<br/>Number of write retries</summary>
    public ulong RetriesWritten { get; internal set; }

    /// <summary>読み取りエラー回数<br/>Number of read errors</summary>
    public ulong ErrorsRead { get; internal set; }

    /// <summary>書き込みエラー回数<br/>Number of write errors</summary>
    public ulong ErrorsWritten { get; internal set; }

    /// <summary>読み取りレイテンシ (ナノ秒)<br/>Read latency in nanoseconds</summary>
    public ulong LatencyTimeRead { get; internal set; }

    /// <summary>書き込みレイテンシ (ナノ秒)<br/>Write latency in nanoseconds</summary>
    public ulong LatencyTimeWritten { get; internal set; }

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

    /// <summary>
    /// ディスク容量 (バイト)。IOMedia "Size" プロパティから取得。取得できない場合は 0。
    /// <para>Disk capacity in bytes from the IOMedia "Size" property. 0 if unavailable.</para>
    /// </summary>
    public ulong DiskSize { get; }

    /// <summary>
    /// バス接続種別の文字列。例: "NVMe"、"Apple Fabric"、"SATA"。取得できない場合は null。
    /// <para>Physical bus interconnect string. Example: "NVMe", "Apple Fabric", "SATA". Null if unavailable.</para>
    /// </summary>
    public string? BusType { get; }

    internal DiskDeviceStat(ulong registryEntryId, string name, bool isPhysicalMedium, string? mediaName, string? vendorName, string? mediumType, bool isRemovable, ulong diskSize, string? busType)
    {
        RegistryEntryId = registryEntryId;
        Name = name;
        IsPhysicalMedium = isPhysicalMedium;
        MediaName = mediaName;
        VendorName = string.IsNullOrEmpty(vendorName) ? null : vendorName;
        MediumType = mediumType;
        IsRemovable = isRemovable;
        DiskSize = diskSize;
        BusType = busType;
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
    private readonly bool _includeAll;

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

    private DiskStats(bool includeAll)
    {
        _includeAll = includeAll;
        Update();
    }

    /// <summary>
    /// DiskStats インスタンスを生成する。
    /// <para>Creates a DiskStats instance.</para>
    /// </summary>
    /// <param name="includeAll">
    /// true の場合、APFS コンテナ仮想ディスク (disk3、disk7 等) を含むすべてのディスクを対象にする。
    /// false (デフォルト) の場合、IOKit の親が IOBlockStorageDriver である物理メディアのみを対象にする。
    /// <para>
    /// When true, all disks are included, including APFS container virtual disks (e.g. disk3, disk7).
    /// When false (default), only physical media whose IOKit parent is IOBlockStorageDriver are included.
    /// </para>
    /// </param>
    public static DiskStats Create(bool includeAll = false) => new(includeAll);

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
                    var bsdName = GetBsdNameIfWhole(entry);
                    if (bsdName is null)
                    {
                        continue;
                    }

                    var parent = GetParentEntry(entry);
                    if (parent == 0)
                    {
                        continue;
                    }

                    try
                    {
                        var isPhysical = IsPhysicalMedium(parent);

                        // includeAll=false (デフォルト) の場合、物理メディアのみを対象にする
                        if (!_includeAll && !isPhysical)
                        {
                            continue;
                        }

                        // Statistics カウンタを保持している親エントリの ID で同一判定する。
                        // BSD 名はデバイス抜き差し後に再利用される可能性があるが、
                        // Registry Entry ID はエントリが生存している間だけ有効で、
                        // 再挿入時は新しい ID が付与されるため、同一カウンタの追跡に適している。
                        if (IORegistryEntryGetRegistryEntryID(parent, out var entryId) != KERN_SUCCESS)
                        {
                            continue;
                        }

                        var device = default(DiskDeviceStat);
                        foreach (var item in _devices)
                        {
                            if (item.RegistryEntryId == entryId)
                            {
                                device = item;
                                break;
                            }
                        }

                        if (device is null)
                        {
                            // 新規デバイス: 静的情報を一度だけ取得してコンストラクタに渡す
                            var (isRemovable, mediaName, vendorName, mediumType, diskSize, busType) = ReadStaticDeviceInfo(entry);
                            device = new DiskDeviceStat(entryId, bsdName, isPhysical, mediaName, vendorName, mediumType, isRemovable, diskSize, busType);
                            _devices.Add(device);
                            added = true;
                        }

                        device.Live = true;
                        ReadStats(parent, device);
                    }
                    finally
                    {
                        IOObjectRelease(parent);
                    }
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
    // Private types / helpers
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOMedia エントリが Whole=true の場合に BSD 名を返す。
    /// Whole でない場合、または BSD 名が取得できない場合は null を返す。
    /// </summary>
    private static string? GetBsdNameIfWhole(uint mediaEntry)
    {
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

        return GetIokitString(mediaEntry, "BSD Name");
    }

    /// <summary>
    /// IOMedia エントリの IOService プレーンにおける親エントリを返す。
    /// 取得失敗時は 0 を返す。戻り値が非 0 の場合、呼び出し元が IOObjectRelease しなければならない。
    /// </summary>
    private static uint GetParentEntry(uint mediaEntry)
    {
        return IORegistryEntryGetParentEntry(mediaEntry, "IOService", out var parent) == KERN_SUCCESS && parent != 0
            ? parent
            : 0;
    }

    /// <summary>
    /// 親エントリのクラスが IOBlockStorageDriver かどうかを返す。
    /// true の場合、物理ブロックストレージデバイス (NVMe、USB NVMe など)。
    /// false の場合、APFS コンテナ仮想ディスクなど合成された論理ディスク。
    /// </summary>
    private static bool IsPhysicalMedium(uint parentEntry)
    {
        return GetIokitClassName(parentEntry) == "IOBlockStorageDriver";
    }

    /// <summary>
    /// 親エントリの Statistics ディクショナリを読み取り、device のプロパティを直接更新する。
    /// </summary>
    private static void ReadStats(uint parentEntry, DiskDeviceStat device)
    {
        var statsDict = GetIokitDictionary(parentEntry, "Statistics");
        if (statsDict == IntPtr.Zero)
        {
            return;
        }

        try
        {
            device.BytesRead          = GetIokitDictUInt64(statsDict, "Bytes (Read)");
            device.BytesWritten       = GetIokitDictUInt64(statsDict, "Bytes (Write)");
            device.ReadsCompleted     = GetIokitDictUInt64(statsDict, "Operations (Read)");
            device.WritesCompleted    = GetIokitDictUInt64(statsDict, "Operations (Write)");
            device.TotalTimeRead      = GetIokitDictUInt64(statsDict, "Total Time (Read)");
            device.TotalTimeWritten   = GetIokitDictUInt64(statsDict, "Total Time (Write)");
            device.RetriesRead        = GetIokitDictUInt64(statsDict, "Retries (Read)");
            device.RetriesWritten     = GetIokitDictUInt64(statsDict, "Retries (Write)");
            device.ErrorsRead         = GetIokitDictUInt64(statsDict, "Errors (Read)");
            device.ErrorsWritten      = GetIokitDictUInt64(statsDict, "Errors (Write)");
            device.LatencyTimeRead    = GetIokitDictUInt64(statsDict, "Latency Time (Read)");
            device.LatencyTimeWritten = GetIokitDictUInt64(statsDict, "Latency Time (Write)");
        }
        finally
        {
            CFRelease(statsDict);
        }
    }

    /// <summary>
    /// IOMedia エントリから静的なデバイス情報を読み取る。新規デバイス登録時に一度だけ呼ばれる。
    /// </summary>
    private static (bool IsRemovable, string? MediaName, string? VendorName, string? MediumType, ulong DiskSize, string? BusType) ReadStaticDeviceInfo(uint mediaEntry)
    {
        var isRemovable = GetIokitBoolean(mediaEntry, "Removable");
        var diskSize = GetIokitUInt64(mediaEntry, "Size");
        var (mediaName, vendorName, mediumType, busType) = FindDeviceCharacteristics(mediaEntry);
        return (isRemovable, mediaName, vendorName, mediumType, diskSize, busType);
    }

    /// <summary>
    /// IOKit エントリの祖先を IOService プレーンで上方向に辿り、
    /// "Device Characteristics" 辞書中の製品名・ベンダー名・メディア種別と
    /// "Protocol Characteristics" 辞書中のバス接続種別を返す。
    /// entry 自体は呼び出し元が所有するため release しない。
    /// </summary>
    private static (string? MediaName, string? VendorName, string? MediumType, string? BusType) FindDeviceCharacteristics(uint entry)
    {
        var current = entry;
        var shouldReleaseCurrent = false;

        string? mediaName = null, vendorName = null, mediumType = null, busType = null;

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

            if (mediaName is null)
            {
                var deviceCharacts = GetIokitDictionary(current, "Device Characteristics");
                if (deviceCharacts != IntPtr.Zero)
                {
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
                }
            }

            if (busType is null)
            {
                var protoCharacts = GetIokitDictionary(current, "Protocol Characteristics");
                if (protoCharacts != IntPtr.Zero)
                {
                    try
                    {
                        busType = GetIokitDictString(protoCharacts, "Physical Interconnect");
                    }
                    finally
                    {
                        CFRelease(protoCharacts);
                    }
                }
            }

            // 両方取得できたら早期終了
            if (mediaName is not null && busType is not null)
            {
                break;
            }
        }

        if (shouldReleaseCurrent)
        {
            IOObjectRelease(current);
        }

        return (mediaName, vendorName, mediumType, busType);
    }
}
