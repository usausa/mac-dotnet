namespace MacDotNet.Disk;

// ReSharper disable IdentifierTypo
public enum BusType
{
    Unknown = 0,
    Nvme,
    AppleFabric,
    Ata,
    Sata,
    Atapi,
    Usb,
    FibreChannel,
    FireWire,
    Thunderbolt,
    SdCard,
    Virtual
}
// ReSharper restore IdentifierTypo

public enum MediumType
{
    Unknown = 0,
    SolidState,
    Rotational
}

public enum BusLocation
{
    Unknown = 0,
    Internal,
    External,
    File
}

public enum ContentType
{
    Unknown = 0,
    GuidPartitionScheme,
    ApplePartitionScheme,
    FDiskPartitionScheme,
    AppleApfs
}

public interface IDiskInfo : IDisposable
{
    public uint Index { get; }

    public string? BsdName { get; }

    public string? DeviceName { get; }

    public string Model { get; }

    public string SerialNumber { get; }

    public string FirmwareRevision { get; }

    public MediumType MediumType { get; }

    public ulong Size { get; }

    public uint PhysicalBlockSize { get; }

    public uint LogicalBlockSize { get; }

    public bool Removable { get; }

    public bool Ejectable { get; }

    public BusType BusType { get; }

    public BusLocation BusLocation { get; }

    public ContentType ContentType { get; }

    public SmartType SmartType { get; }

    public ISmart Smart { get; }
}
