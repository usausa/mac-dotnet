namespace MacDotNet.Disk;

// IOKit Protocol Characteristicsから取得されるバス種別
public enum BusType
{
    Unknown = 0,

    // NVMe接続
    Nvme,

    // Apple Fabric接続 (Apple Silicon内蔵SSD)
    AppleFabric,

    // ATA接続
    Ata,

    // SATA接続
    Sata,

    // ATAPI接続
    Atapi,

    // USB接続
    Usb,

    // Fibre Channel接続
    FibreChannel,

    // FireWire (IEEE 1394) 接続
    FireWire,

    // Thunderbolt接続
    Thunderbolt,

    // SD/MMCカード
    SdCard,

    // Virtual
    Virtual
}
