namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public enum DiskBusType
{
    Unknown,
    VirtualInterface,
    AppleFabric,
    Sata,
    PciExpress,
    Usb,
    Thunderbolt,
    FireWire,
    Sas,
    Sd
}

public sealed class DiskDeviceStat
{
    internal bool Live { get; set; }

    internal bool Target { get; set; }

    internal ulong RegistryEntryId { get; }

    // Interface

    public string BsdName { get; }

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
        BsdName = name;
        BusType = busType;
        IsPhysical = isPhysicalMedium;
        IsRemovable = isRemovable;
        DiskSize = diskSize;
        MediaName = mediaName;
        VendorName = vendorName;
        MediumType = mediumType;
    }
}

public sealed class DiskStat
{
    private readonly bool includeAll;

    private readonly List<DiskDeviceStat> devices = [];

    private readonly List<DiskDeviceStat> filteredDevices = [];

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<DiskDeviceStat> Devices => includeAll ? devices : filteredDevices;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal DiskStat(bool includeAll = false)
    {
        this.includeAll = includeAll;
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

        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), out var itHandle);
        if ((kr != KERN_SUCCESS) || (itHandle == 0))
        {
            return false;
        }

        var added = false;
        var filterAdded = false;

        uint rawEntry;
        using var it = new IORef(itHandle);
        while ((rawEntry = IOIteratorNext(it)) != 0)
        {
            using var entry = new IOObj(rawEntry);
            if (!entry.GetBoolean("Whole"))
            {
                continue;
            }

            if ((IORegistryEntryGetParentEntry(entry, "IOService", out var rawParent) != KERN_SUCCESS) || (rawParent == 0))
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
                device.Target = includeAll || (device.IsPhysical && (device.BusType != DiskBusType.VirtualInterface));

                devices.Add(device);
                added = true;

                if (!includeAll && device.Target)
                {
                    filteredDevices.Add(device);
                    filterAdded = true;
                }
            }

            if (device.Target)
            {
                ReadStatistics(parent, device);
            }

            device.Live = true;
        }

        for (var i = devices.Count - 1; i >= 0; i--)
        {
            var device = devices[i];
            if (!device.Live)
            {
                if (device.Target)
                {
                    filteredDevices.Remove(device);
                }
                devices.RemoveAt(i);
            }
        }

        if (added)
        {
            devices.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.BsdName, y.BsdName));
            if (filterAdded)
            {
                filteredDevices.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.BsdName, y.BsdName));
            }
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
            "Virtual Interface" => DiskBusType.VirtualInterface,
            "Apple Fabric" => DiskBusType.AppleFabric,
            "SATA" => DiskBusType.Sata,
            "PCI-Express" => DiskBusType.PciExpress,
            "USB" => DiskBusType.Usb,
            "Thunderbolt" => DiskBusType.Thunderbolt,
            "FireWire" => DiskBusType.FireWire,
            "SAS" => DiskBusType.Sas,
            "SD" => DiskBusType.Sd,
            _ => DiskBusType.Unknown
        };

    private static (string? MediaName, string? VendorName, string? MediumType, string? BusType) FindDeviceCharacteristics(uint entry)
    {
        var busType = default(string);
        var mediaName = default(string);
        var vendorName = default(string);
        var mediumType = default(string);

        var ioObj = IOObj.Zero;
        try
        {
            for (var depth = 0; depth < 8; depth++)
            {
                if (IORegistryEntryGetParentEntry(entry, "IOService", out var parent) != KERN_SUCCESS || parent == 0)
                {
                    break;
                }

                ioObj.Dispose();
                ioObj = new IOObj(parent);
                entry = parent;

                if (busType is null)
                {
                    using var protocol = ioObj.GetDictionary("Protocol Characteristics");
                    if (protocol.IsValid)
                    {
                        busType = protocol.GetString("Physical Interconnect");
                    }
                }

                if (mediaName is null)
                {
                    using var device = ioObj.GetDictionary("Device Characteristics");
                    if (device.IsValid)
                    {
                        mediaName = device.GetString("Product Name");
                        vendorName = device.GetString("Vendor Name");
                        mediumType = device.GetString("Medium Type");
                    }
                }

                if ((mediaName is not null) && (busType is not null))
                {
                    break;
                }
            }
        }
        finally
        {
            ioObj.Dispose();
        }

        return (mediaName, vendorName, mediumType, busType);
    }
}
