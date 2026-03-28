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
    uint Index { get; }

    string? BsdName { get; }

    string? DeviceName { get; }

    string Model { get; }

    string SerialNumber { get; }

    string FirmwareRevision { get; }

    MediumType MediumType { get; }

    ulong Size { get; }

    uint PhysicalBlockSize { get; }

    uint LogicalBlockSize { get; }

    bool Removable { get; }

    bool Ejectable { get; }

    BusType BusType { get; }

    BusLocation BusLocation { get; }

    ContentType ContentType { get; }

    SmartType SmartType { get; }

    ISmart Smart { get; }
}
