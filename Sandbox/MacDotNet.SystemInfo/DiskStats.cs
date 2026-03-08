namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public enum DiskBusType
{
    Unknown,
    Sata,
    PciExpress,
    Usb,
    Thunderbolt,
    FireWire,
    Sas,
    Sd,
    AppleFabric
}

public sealed class DiskDeviceStat
{
    internal bool Live { get; set; }

    internal ulong RegistryEntryId { get; }

    // Interface

    public string Name { get; }

    // Statistics

    public ulong BytesRead { get; internal set; }

    public ulong BytesWrite { get; internal set; }

    public ulong ReadsCompleted { get; internal set; }

    public ulong WritesCompleted { get; internal set; }

    public ulong TotalTimeRead { get; internal set; }

    public ulong TotalTimeWrite { get; internal set; }

    public ulong RetriesRead { get; internal set; }

    public ulong RetriesWrite { get; internal set; }

    public ulong ErrorsRead { get; internal set; }

    public ulong ErrorsWrite { get; internal set; }

    public ulong LatencyTimeRead { get; internal set; }

    public ulong LatencyTimeWrite { get; internal set; }

    // ---- 静的情報 (インスタンス作成時に一度だけ取得) ----

    public bool IsPhysical { get; }

    public bool IsRemovable { get; }

    public DiskBusType BusType { get; }

    public ulong DiskSize { get; }

    public string? MediaName { get; }

    public string? VendorName { get; }

    public string? MediumType { get; }

    internal DiskDeviceStat(ulong registryEntryId, string name, bool isPhysicalMedium, bool isRemovable, DiskBusType busType, ulong diskSize, string? mediaName, string? vendorName, string? mediumType)
    {
        RegistryEntryId = registryEntryId;
        Name = name;
        IsPhysical = isPhysicalMedium;
        IsRemovable = isRemovable;
        BusType = busType;
        DiskSize = diskSize;
        MediaName = mediaName;
        VendorName = vendorName;
        MediumType = mediumType;
    }
}

public sealed class DiskStats
{
    private readonly List<DiskDeviceStat> devices = new();

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<DiskDeviceStat> Devices => devices;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal DiskStats()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        foreach (var device in devices)
        {
            device.Live = false;
        }

        var iterPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), ref iterPtr);
        if (kr != KERN_SUCCESS || iterPtr == IntPtr.Zero)
        {
            return false;
        }

        using var iter = new IORef(iterPtr);
        var added = false;
        uint rawEntry;
        while ((rawEntry = IOIteratorNext(iter)) != 0)
        {
            using var entry = new IOObj(rawEntry);
            if (!IsWhole(entry))
            {
                continue;
            }

            var rawParent = GetParentEntry(entry);
            if (rawParent == 0)
            {
                continue;
            }

            using var parent = new IOObj(rawParent);
            if (IORegistryEntryGetRegistryEntryID(parent, out var entryId) != KERN_SUCCESS)
            {
                continue;
            }

            var device = default(DiskDeviceStat);
            foreach (var item in devices)
            {
                if (item.RegistryEntryId == entryId)
                {
                    device = item;
                    break;
                }
            }

            if (device is null)
            {
                device = CreateEntry(entryId, entry, parent);
                devices.Add(device);
                added = true;
            }

            device.Live = true;
            ReadStatistics(parent, device);
        }

        for (var i = devices.Count - 1; i >= 0; i--)
        {
            if (!devices[i].Live)
            {
                devices.RemoveAt(i);
            }
        }

        if (added)
        {
            devices.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.Name, y.Name));
        }

        UpdateAt = DateTime.Now;
        return true;
    }

    /// <summary>
    /// 新規デバイス検出時に IOKit から静的情報を取得してエントリを生成する。
    /// IOMedia エントリが Whole でない場合は null を返す。
    /// </summary>
    private static DiskDeviceStat CreateEntry(ulong registryEntryId, IOObj mediaEntry, IOObj parentEntry)
    {
        var bsdName = mediaEntry.GetString("BSD Name") ?? string.Empty;
        var isPhysical = IsPhysical(parentEntry);
        var isRemovable = mediaEntry.GetBoolean("Removable");
        var diskSize = mediaEntry.GetUInt64("Size");
        var (mediaName, vendorName, mediumType, busTypeStr) = FindDeviceCharacteristics(mediaEntry);
        return new DiskDeviceStat(registryEntryId, bsdName, isPhysical, isRemovable, ParseBusType(busTypeStr), diskSize, mediaName, vendorName, mediumType);
    }

    private static void ReadStatistics(IOObj parentEntry, DiskDeviceStat device)
    {
        using var statsDict = parentEntry.GetDictionary("Statistics");
        if (!statsDict.IsValid)
        {
            return;
        }

        device.BytesRead = statsDict.GetUInt64("Bytes (Read)");
        device.BytesWrite = statsDict.GetUInt64("Bytes (Write)");
        device.ReadsCompleted = statsDict.GetUInt64("Operations (Read)");
        device.WritesCompleted = statsDict.GetUInt64("Operations (Write)");
        device.TotalTimeRead = statsDict.GetUInt64("Total Time (Read)");
        device.TotalTimeWrite = statsDict.GetUInt64("Total Time (Write)");
        device.RetriesRead = statsDict.GetUInt64("Retries (Read)");
        device.RetriesWrite = statsDict.GetUInt64("Retries (Write)");
        device.ErrorsRead = statsDict.GetUInt64("Errors (Read)");
        device.ErrorsWrite = statsDict.GetUInt64("Errors (Write)");
        device.LatencyTimeRead = statsDict.GetUInt64("Latency Time (Read)");
        device.LatencyTimeWrite = statsDict.GetUInt64("Latency Time (Write)");
    }

    //--------------------------------------------------------------------------------
    // Private types / helpers
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOMedia エントリが Whole=true かどうかを返す。
    /// </summary>
    private static bool IsWhole(IOObj mediaEntry) => mediaEntry.GetBoolean("Whole");

    /// <summary>
    /// IOMedia エントリの IOService プレーンにおける親エントリを返す。
    /// 取得失敗時は 0 を返す。戻り値が非 0 の場合、呼び出し元が IOObjectRelease しなければならない。
    /// </summary>
    private static uint GetParentEntry(IOObj mediaEntry)
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
    private static bool IsPhysical(IOObj parentEntry)
    {
        return parentEntry.GetClassName() == "IOBlockStorageDriver";
    }

    private static DiskBusType ParseBusType(string? busType) =>
        busType switch
        {
            "SATA" => DiskBusType.Sata,
            "PCI-Express" => DiskBusType.PciExpress,
            "USB" => DiskBusType.Usb,
            "Thunderbolt" => DiskBusType.Thunderbolt,
            "FireWire" => DiskBusType.FireWire,
            "SAS" => DiskBusType.Sas,
            "SD" => DiskBusType.Sd,
            "Apple Fabric" => DiskBusType.AppleFabric,
            _ => DiskBusType.Unknown
        };

    /// <summary>
    /// IOKit エントリの祖先を IOService プレーンで上方向に辿り、
    /// "Device Characteristics" 辞書中の製品名・ベンダー名・メディア種別と
    /// "Protocol Characteristics" 辞書中のバス接続種別を返す。
    /// entry 自体は呼び出し元が所有するため release しない。
    /// </summary>
    private static (string? MediaName, string? VendorName, string? MediumType, string? BusType) FindDeviceCharacteristics(IOObj entry)
    {
        uint currentRaw = entry;
        var currentOwned = IOObj.Zero;

        string? mediaName = null, vendorName = null, mediumType = null, busType = null;

        for (var depth = 0; depth < 8; depth++)
        {
            if (IORegistryEntryGetParentEntry(currentRaw, "IOService", out var parent) != KERN_SUCCESS || parent == 0)
            {
                break;
            }

            currentOwned.Dispose();
            currentOwned = new IOObj(parent);
            currentRaw = parent;

            if (mediaName is null)
            {
                using var deviceCharacts = currentOwned.GetDictionary("Device Characteristics");
                if (deviceCharacts.IsValid)
                {
                    mediaName = deviceCharacts.GetString("Product Name");
                    vendorName = deviceCharacts.GetString("Vendor Name");
                    mediumType = deviceCharacts.GetString("Medium Type");
                }
            }

            if (busType is null)
            {
                using var protoCharacts = currentOwned.GetDictionary("Protocol Characteristics");
                if (protoCharacts.IsValid)
                {
                    busType = protoCharacts.GetString("Physical Interconnect");
                }
            }

            // 両方取得できたら早期終了
            if (mediaName is not null && busType is not null)
            {
                break;
            }
        }

        currentOwned.Dispose();

        return (mediaName, vendorName, mediumType, busType);
    }
}
