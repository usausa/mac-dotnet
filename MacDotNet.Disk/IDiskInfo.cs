namespace MacDotNet.Disk;

public interface IDiskInfo : IDisposable
{
    public uint Index { get; }

    // BSDデバイス名 (例: "disk0")
    public string? BsdName { get; }

    // IORegistryエントリ名
    public string? DeviceName { get; }

    public string Model { get; }

    public string SerialNumber { get; }

    public string FirmwareRevision { get; }

    // メディアタイプ (Solid State / Rotational 等)
    public string? MediumType { get; }

    public ulong Size { get; }

    public uint PhysicalBlockSize { get; }

    public uint LogicalBlockSize { get; }

    public bool Removable { get; }

    // メディア取り出し可能か (Ejectable)
    public bool Ejectable { get; }

    public BusType BusType { get; }

    // 接続ロケーション (Internal, External 等)
    public string? BusLocation { get; }

    // コンテントタイプ (GUID_partition_scheme, Apple_APFS 等)
    public string? ContentType { get; }

    public SmartType SmartType { get; }

    public ISmart Smart { get; }

    // I/O統計情報
    public DiskIOStatistics? IOStatistics { get; }
}
