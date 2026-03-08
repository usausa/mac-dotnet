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

    // Information

    public DiskBusType BusType { get; }

    public bool IsPhysical { get; }

    public bool IsRemovable { get; }

    public ulong DiskSize { get; }

    public string? MediaName { get; }

    public string? VendorName { get; }

    public string? MediumType { get; }

    internal DiskDeviceStat(ulong registryEntryId, string name, DiskBusType busType, bool isPhysicalMedium, bool isRemovable, ulong diskSize, string? mediaName, string? vendorName, string? mediumType)
    {
        RegistryEntryId = registryEntryId;
        Name = name;
        BusType = busType;
        IsPhysical = isPhysicalMedium;
        IsRemovable = isRemovable;
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

        var itPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), ref itPtr);
        if ((kr != KERN_SUCCESS) || (itPtr == IntPtr.Zero))
        {
            return false;
        }

        var added = false;
        uint rawEntry;
        using var it = new IORef(itPtr);
        while ((rawEntry = IOIteratorNext(it)) != 0)
        {
            using var entry = new IOObj(rawEntry);
            if (!entry.GetBoolean("Whole"))
            {
                continue;
            }

            if (IORegistryEntryGetParentEntry(entry, "IOService", out var rawParent) != KERN_SUCCESS || rawParent == 0)
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

    private static void ReadStatistics(IOObj parentEntry, DiskDeviceStat device)
    {
        using var statistics = parentEntry.GetDictionary("Statistics");
        if (!statistics.IsValid)
        {
            return;
        }

        device.BytesRead = statistics.GetUInt64("Bytes (Read)");
        device.BytesWrite = statistics.GetUInt64("Bytes (Write)");
        device.ReadsCompleted = statistics.GetUInt64("Operations (Read)");
        device.WritesCompleted = statistics.GetUInt64("Operations (Write)");
        device.TotalTimeRead = statistics.GetUInt64("Total Time (Read)");
        device.TotalTimeWrite = statistics.GetUInt64("Total Time (Write)");
        device.RetriesRead = statistics.GetUInt64("Retries (Read)");
        device.RetriesWrite = statistics.GetUInt64("Retries (Write)");
        device.ErrorsRead = statistics.GetUInt64("Errors (Read)");
        device.ErrorsWrite = statistics.GetUInt64("Errors (Write)");
        device.LatencyTimeRead = statistics.GetUInt64("Latency Time (Read)");
        device.LatencyTimeWrite = statistics.GetUInt64("Latency Time (Write)");
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static DiskDeviceStat CreateEntry(ulong registryEntryId, IOObj entry, IOObj parentEntry)
    {
        var bsdName = entry.GetString("BSD Name") ?? string.Empty;
        var isPhysical = parentEntry.GetClassName() == "IOBlockStorageDriver";
        var isRemovable = entry.GetBoolean("Removable");
        var diskSize = entry.GetUInt64("Size");
        var (mediaName, vendorName, mediumType, busTypeStr) = FindDeviceCharacteristics(entry);
        var busType = ParseBusType(busTypeStr);
        return new DiskDeviceStat(registryEntryId, bsdName, busType, isPhysical, isRemovable, diskSize, mediaName, vendorName, mediumType);
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

            if (busType is null)
            {
                using var protoCharacts = currentOwned.GetDictionary("Protocol Characteristics");
                if (protoCharacts.IsValid)
                {
                    busType = protoCharacts.GetString("Physical Interconnect");
                }
            }

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
