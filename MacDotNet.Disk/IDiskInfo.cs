namespace MacDotNet.Disk;

public interface IDiskInfo : IDisposable
{
    public uint Index { get; }

    public string? BsdName { get; }

    public string? DeviceName { get; }

    public string Model { get; }

    public string SerialNumber { get; }

    public string FirmwareRevision { get; }

    public string? MediumType { get; }

    public ulong Size { get; }

    public uint PhysicalBlockSize { get; }

    public uint LogicalBlockSize { get; }

    public bool Removable { get; }

    public bool Ejectable { get; }

    public BusType BusType { get; }

    public string? BusLocation { get; }

    public string? ContentType { get; }

    public SmartType SmartType { get; }

    public ISmart Smart { get; }
}
