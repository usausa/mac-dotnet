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
    Sd,
    Ata,
    Atapi,
    FibreChannel,
    Nvme
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

public sealed class DiskStat : IDisposable
{
    // IOMedia
    private static readonly IntPtr WholeKey = CFSTR("Whole");

    // Statistics
    private static readonly IntPtr StatisticsKey = CFSTR("Statistics");
    private static readonly IntPtr BytesReadKey = CFSTR("Bytes (Read)");
    private static readonly IntPtr BytesWriteKey = CFSTR("Bytes (Write)");
    private static readonly IntPtr OperationsReadKey = CFSTR("Operations (Read)");
    private static readonly IntPtr OperationsWriteKey = CFSTR("Operations (Write)");
    private static readonly IntPtr TotalTimeReadKey = CFSTR("Total Time (Read)");
    private static readonly IntPtr TotalTimeWriteKey = CFSTR("Total Time (Write)");
    private static readonly IntPtr RetriesReadKey = CFSTR("Retries (Read)");
    private static readonly IntPtr RetriesWriteKey = CFSTR("Retries (Write)");
    private static readonly IntPtr ErrorsReadKey = CFSTR("Errors (Read)");
    private static readonly IntPtr ErrorsWriteKey = CFSTR("Errors (Write)");
    private static readonly IntPtr LatencyTimeReadKey = CFSTR("Latency Time (Read)");
    private static readonly IntPtr LatencyTimeWriteKey = CFSTR("Latency Time (Write)");

    private readonly bool includeAll;

    private readonly List<DiskDeviceStat> devices = [];

    private readonly List<DiskDeviceStat> filteredDevices = [];

    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<DiskDeviceStat> Devices => includeAll ? devices : filteredDevices;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private DiskStat(bool includeAll)
    {
        this.includeAll = includeAll;
        Update();
    }

    internal static DiskStat Create(bool includeAll = false) => new(includeAll);

    public void Dispose()
    {
        disposed = true;
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

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
            if (!entry.GetBoolean(WholeKey))
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
        using var statistics = parentEntry.GetDictionary(StatisticsKey);
        if (!statistics.IsValid)
        {
            return;
        }

        device.BytesRead = statistics.GetUInt64(BytesReadKey);
        device.BytesWrite = statistics.GetUInt64(BytesWriteKey);
        device.ReadsCompleted = statistics.GetUInt64(OperationsReadKey);
        device.WritesCompleted = statistics.GetUInt64(OperationsWriteKey);
        device.TotalTimeRead = statistics.GetUInt64(TotalTimeReadKey);
        device.TotalTimeWrite = statistics.GetUInt64(TotalTimeWriteKey);
        device.RetriesRead = statistics.GetUInt64(RetriesReadKey);
        device.RetriesWrite = statistics.GetUInt64(RetriesWriteKey);
        device.ErrorsRead = statistics.GetUInt64(ErrorsReadKey);
        device.ErrorsWrite = statistics.GetUInt64(ErrorsWriteKey);
        device.LatencyTimeRead = statistics.GetUInt64(LatencyTimeReadKey);
        device.LatencyTimeWrite = statistics.GetUInt64(LatencyTimeWriteKey);
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
            "NVMe" => DiskBusType.Nvme,
            "ATA" => DiskBusType.Ata,
            "SATA" => DiskBusType.Sata,
            "ATAPI" => DiskBusType.Atapi,
            "PCI-Express" => DiskBusType.PciExpress,
            "USB" => DiskBusType.Usb,
            "Fibre Channel" => DiskBusType.FibreChannel,
            "Thunderbolt" => DiskBusType.Thunderbolt,
            "FireWire" => DiskBusType.FireWire,
            "SAS" => DiskBusType.Sas,
            "Secure Digital" or "SD" => DiskBusType.Sd,
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
                if ((IORegistryEntryGetParentEntry(entry, "IOService", out var parent) != KERN_SUCCESS) || (parent == 0))
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
