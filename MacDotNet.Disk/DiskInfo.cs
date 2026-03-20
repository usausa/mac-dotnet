namespace MacDotNet.Disk;

using System.Runtime.InteropServices;

using static MacDotNet.Disk.NativeMethods;

public static class DiskInfo
{
    public static IReadOnlyList<IDiskInfo> GetInformation()
    {
        var matching = IOServiceMatching("IOBlockStorageDevice");
        if (matching == IntPtr.Zero)
        {
            return [];
        }

        var itHandle = 0u;
        if ((IOServiceGetMatchingServices(0, matching, ref itHandle) != KERN_SUCCESS) || (itHandle == 0))
        {
            return [];
        }

        var list = new List<IDiskInfo>();

        var index = 0u;
        using var it = new IOObj(itHandle);
        uint entryHandle;
        while ((entryHandle = IOIteratorNext(it)) != 0u)
        {
#pragma warning disable CA2000
            using var entry = new IOObj(entryHandle);
#pragma warning restore CA2000
            list.Add(ReadDiskEntry(entry, index++));
        }

        return list;
    }

    // TODO Check
    private static unsafe DiskInfoGeneric ReadDiskEntry(IOObj entry, uint index)
    {
        var nameBuf = stackalloc byte[128];
        _ = IORegistryEntryGetName(entry, nameBuf);
        var deviceName = Marshal.PtrToStringUTF8((IntPtr)nameBuf);

        // Device Characteristics
        string? modelName = null;
        string? vendorName = null;
        string? serialNumber = null;
        string? firmwareRevision = null;
        string? mediumType = null;
        using var devCharDict = entry.GetDictionary("Device Characteristics");
        if (devCharDict.IsValid)
        {
            modelName = devCharDict.GetString("Product Name");
            vendorName = devCharDict.GetString("Vendor Name");
            serialNumber = devCharDict.GetString("Serial Number");
            firmwareRevision = devCharDict.GetString("Product Revision Level");
            mediumType = devCharDict.GetString("Medium Type");
        }

        // Protocol Characteristics
        string? physInterconnect = null;
        string? physInterconnectLocation = null;
        using var protoCharDict = entry.GetDictionary("Protocol Characteristics");
        if (protoCharDict.IsValid)
        {
            physInterconnect = protoCharDict.GetString("Physical Interconnect");
            physInterconnectLocation = protoCharDict.GetString("Physical Interconnect Location");
        }

        // 子エントリを再帰検索してIOMedia/IOBlockStorageDriverのプロパティを取得
        // Recursively search child entries to obtain IOMedia / IOBlockStorageDriver properties
        var bsdName = entry.SearchString("BSD Name");
        var diskSize = entry.SearchInt64("Size");
        var logicalBlockSize = entry.SearchInt64("Preferred Block Size");
        var physicalBlockSize = entry.SearchInt64("Physical Block Size");
        var removable = entry.SearchBool("Removable");
        var ejectable = entry.SearchBool("Ejectable");
        var contentType = entry.SearchString("Content");

        // Fall back to logical block size when physical block size is unavailable
        if (physicalBlockSize <= 0 && logicalBlockSize > 0)
        {
            physicalBlockSize = logicalBlockSize;
        }

        // モデル名の構築 (ベンダ名がある場合は結合)
        // Build the model string (concatenate vendor name if present)
        var model = BuildModelString(modelName, vendorName);
        var busType = ParseBusType(physInterconnect);
        var medium = ParseMediumType(mediumType?.Trim());
        var busLocation = ParseBusLocation(physInterconnectLocation);
        var content = ParseContentType(contentType);

        // Smart
        SmartType smartType;
        ISmart smart;

        if (busType is BusType.Nvme or BusType.AppleFabric)
        {
            var session = SmartNvme.Open(entry);
            if (session is not null && session.Update())
            {
                smartType = SmartType.Nvme;
                smart = session;
            }
            else
            {
                session?.Dispose();
                smartType = SmartType.Unsupported;
                smart = SmartUnsupported.Default;
            }
        }
        else if (busType is BusType.Ata or BusType.Sata or BusType.Atapi)
        {
            var session = SmartGeneric.Open(entry);
            if (session is not null && session.Update())
            {
                smartType = SmartType.Generic;
                smart = session;
            }
            else
            {
                session?.Dispose();
                smartType = SmartType.Unsupported;
                smart = SmartUnsupported.Default;
            }
        }
        else
        {
            smartType = SmartType.Unsupported;
            smart = SmartUnsupported.Default;
        }

        try
        {
            return new DiskInfoGeneric
            {
                Index = index,
                BsdName = bsdName ?? string.Empty,
                DeviceName = deviceName ?? string.Empty,
                Model = model,
                SerialNumber = serialNumber?.Trim() ?? string.Empty,
                FirmwareRevision = firmwareRevision?.Trim() ?? string.Empty,
                MediumType = medium,
                Removable = removable,
                Ejectable = ejectable,
                PhysicalBlockSize = physicalBlockSize > 0 ? (uint)physicalBlockSize : 0,
                LogicalBlockSize = logicalBlockSize > 0 ? (uint)logicalBlockSize : 0,
                Size = diskSize > 0 ? (ulong)diskSize : 0,
                BusType = busType,
                BusLocation = busLocation,
                ContentType = content,
                SmartType = smartType,
                Smart = smart
            };
        }
        catch
        {
            (smart as IDisposable)?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// モデル文字列を構築する (ベンダ名がある場合は結合)。
    /// Builds the model string, concatenating vendor name if present.
    /// </summary>
    private static string BuildModelString(string? modelName, string? vendorName)
    {
        var model = modelName?.Trim();
        var vendor = vendorName?.Trim();

        if (!string.IsNullOrEmpty(vendor) && !string.IsNullOrEmpty(model))
        {
            return $"{vendor} {model}";
        }

        return model ?? vendor ?? string.Empty;
    }

    // 物理接続文字列からバス種別を判定する
    // Determines the bus type from the physical interconnect string
    // ReSharper disable StringLitalTypo
    private static BusType ParseBusType(string? physInterconnect) => physInterconnect switch
    {
        "NVMe" => BusType.Nvme,
        "Apple Fabric" => BusType.AppleFabric,
        "ATA" => BusType.Ata,
        "SATA" => BusType.Sata,
        "ATAPI" => BusType.Atapi,
        "USB" => BusType.Usb,
        "Fibre Channel" => BusType.FibreChannel,
        "FireWire" => BusType.FireWire,
        "Thunderbolt" => BusType.Thunderbolt,
        "Secure Digital" => BusType.SdCard,
        "Virtual Interface" => BusType.Virtual,
        _ => BusType.Unknown
    };
    // ReSharper restore StringLitalTypo

    // メディアタイプ文字列からMediumType列挙値を判定する
    // Determines the medium type from the medium type string
    private static MediumType ParseMediumType(string? mediumType) => mediumType switch
    {
        "Solid State" => MediumType.SolidState,
        "Rotational" => MediumType.Rotational,
        _ => MediumType.Unknown
    };

    // 接続ロケーション文字列からBusLocation列挙値を判定する
    // Determines the bus location from the physical interconnect location string
    private static BusLocation ParseBusLocation(string? location) => location switch
    {
        "Internal" => BusLocation.Internal,
        "External" => BusLocation.External,
        "File" => BusLocation.File,
        _ => BusLocation.Unknown
    };

    // コンテントタイプ文字列からContentType列挙値を判定する
    // Determines the content type from the content type string
    private static ContentType ParseContentType(string? contentType) => contentType switch
    {
        "GUID_partition_scheme" => ContentType.GuidPartitionScheme,
        "Apple_partition_scheme" => ContentType.ApplePartitionScheme,
        "FDisk_partition_scheme" => ContentType.FDiskPartitionScheme,
        "Apple_APFS" => ContentType.AppleApfs,
        _ => ContentType.Unknown
    };
}
