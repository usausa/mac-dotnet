namespace MacDotNet.Disk;

/// <summary>
/// ディスク情報の汎用実装。
/// Generic implementation of disk information.
/// </summary>
internal sealed class DiskInfoGeneric : IDiskInfo
{
    // ディスクインデックス / Disk index
    public uint Index { get; set; }

    // BSDデバイス名 (例: "disk0") / BSD device name (e.g. "disk0")
    public string? BsdName { get; set; }

    // IORegistryエントリ名 / IORegistry entry name
    public string? DeviceName { get; set; }

    // モデル名 / Model name
    public string Model { get; set; } = string.Empty;

    // シリアル番号 / Serial number
    public string SerialNumber { get; set; } = string.Empty;

    // ファームウェアリビジョン / Firmware revision
    public string FirmwareRevision { get; set; } = string.Empty;

    // メディアタイプ (Solid State / Rotational 等) / Medium type (Solid State / Rotational etc.)
    public string? MediumType { get; set; }

    // ディスク容量 (バイト) / Disk capacity in bytes
    public ulong Size { get; set; }

    // 物理ブロックサイズ / Physical block size
    public uint PhysicalBlockSize { get; set; }

    // 論理ブロックサイズ / Logical block size
    public uint LogicalBlockSize { get; set; }

    // リムーバブルか / Whether the media is removable
    public bool Removable { get; set; }

    // メディア取り出し可能か / Whether the media is ejectable
    public bool Ejectable { get; set; }

    // バス種別 / Bus type
    public BusType BusType { get; set; }

    // 接続ロケーション (Internal, External 等) / Bus location (Internal, External etc.)
    public string? BusLocation { get; set; }

    // コンテントタイプ (GUID_partition_scheme, Apple_APFS 等) / Content type (GUID_partition_scheme, Apple_APFS etc.)
    public string? ContentType { get; set; }

    // SMART種別 / SMART type
    public SmartType SmartType { get; set; }

    // SMARTインターフェース / SMART interface
    public ISmart Smart { get; set; } = SmartUnsupported.Default;

    // I/O統計情報 / I/O statistics
    public DiskIOStatistics? IOStatistics { get; set; }

    /// <summary>
    /// SMARTセッションなどのリソースを解放する。
    /// Releases resources such as the SMART session.
    /// </summary>
    public void Dispose()
    {
        (Smart as IDisposable)?.Dispose();
    }
}
