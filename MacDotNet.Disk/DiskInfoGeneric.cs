namespace MacDotNet.Disk;

internal sealed class DiskInfoGeneric : IDiskInfo
{
    public uint Index { get; set; }

    public string? BsdName { get; set; }

    public string? DeviceName { get; set; }

    public string Model { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;

    public string FirmwareRevision { get; set; } = string.Empty;

    public string? MediumType { get; set; }

    public ulong Size { get; set; }

    public uint PhysicalBlockSize { get; set; }

    public uint LogicalBlockSize { get; set; }

    public bool Removable { get; set; }

    public bool Ejectable { get; set; }

    public BusType BusType { get; set; }

    public string? BusLocation { get; set; }

    public string? ContentType { get; set; }

    public SmartType SmartType { get; set; }

    public ISmart Smart { get; set; } = SmartUnsupported.Default;

    public DiskIOStatistics? IOStatistics { get; set; }

    public void Dispose()
    {
        (Smart as IDisposable)?.Dispose();
    }
}
