namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.IokitHelper;
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
    private static DiskDeviceStat CreateEntry(ulong registryEntryId, uint mediaEntry, uint parentEntry)
    {
        var bsdName = GetIokitString(mediaEntry, "BSD Name") ?? string.Empty;
        var isPhysical = IsPhysical(parentEntry);
        var isRemovable = GetIokitBoolean(mediaEntry, "Removable");
        var diskSize = GetIokitUInt64(mediaEntry, "Size");
        var (mediaName, vendorName, mediumType, busTypeStr) = FindDeviceCharacteristics(mediaEntry);
        return new DiskDeviceStat(registryEntryId, bsdName, isPhysical, isRemovable, ParseBusType(busTypeStr), diskSize, mediaName, vendorName, mediumType);
    }

    private static void ReadStatistics(uint parentEntry, DiskDeviceStat device)
    {
        using var statsDict = GetIokitDictionary(parentEntry, "Statistics");
        if (!statsDict.IsValid)
        {
            return;
        }

        device.BytesRead = GetIokitDictUInt64(statsDict, "Bytes (Read)");
        device.BytesWrite = GetIokitDictUInt64(statsDict, "Bytes (Write)");
        device.ReadsCompleted = GetIokitDictUInt64(statsDict, "Operations (Read)");
        device.WritesCompleted = GetIokitDictUInt64(statsDict, "Operations (Write)");
        device.TotalTimeRead = GetIokitDictUInt64(statsDict, "Total Time (Read)");
        device.TotalTimeWrite = GetIokitDictUInt64(statsDict, "Total Time (Write)");
        device.RetriesRead = GetIokitDictUInt64(statsDict, "Retries (Read)");
        device.RetriesWrite = GetIokitDictUInt64(statsDict, "Retries (Write)");
        device.ErrorsRead = GetIokitDictUInt64(statsDict, "Errors (Read)");
        device.ErrorsWrite = GetIokitDictUInt64(statsDict, "Errors (Write)");
        device.LatencyTimeRead = GetIokitDictUInt64(statsDict, "Latency Time (Read)");
        device.LatencyTimeWrite = GetIokitDictUInt64(statsDict, "Latency Time (Write)");
    }

    //--------------------------------------------------------------------------------
    // Private types / helpers
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOMedia エントリが Whole=true かどうかを返す。
    /// </summary>
    private static bool IsWhole(uint mediaEntry)
    {
        using var wholeKey = CFRef.CreateString("Whole");
        if (!wholeKey.IsValid)
        {
            return false;
        }

        using var wholeProp = new CFRef(IORegistryEntryCreateCFProperty(mediaEntry, wholeKey, IntPtr.Zero, 0));
        return wholeProp.IsValid && CFBooleanGetValue(wholeProp);
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
    private static bool IsPhysical(uint parentEntry)
    {
        return GetIokitClassName(parentEntry) == "IOBlockStorageDriver";
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
    private static (string? MediaName, string? VendorName, string? MediumType, string? BusType) FindDeviceCharacteristics(uint entry)
    {
        var currentRaw = entry;
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
                using var deviceCharacts = GetIokitDictionary(currentRaw, "Device Characteristics");
                if (deviceCharacts.IsValid)
                {
                    mediaName = GetIokitDictString(deviceCharacts, "Product Name");
                    vendorName = GetIokitDictString(deviceCharacts, "Vendor Name");
                    mediumType = GetIokitDictString(deviceCharacts, "Medium Type");
                }
            }

            if (busType is null)
            {
                using var protoCharacts = GetIokitDictionary(currentRaw, "Protocol Characteristics");
                if (protoCharacts.IsValid)
                {
                    busType = GetIokitDictString(protoCharacts, "Physical Interconnect");
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
