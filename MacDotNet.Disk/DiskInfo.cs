namespace MacDotNet.Disk;

using System.Runtime.InteropServices;

using static MacDotNet.Disk.NativeMethods;

// ディスク情報取得処理
// Disk information retrieval.
// IOBlockStorageDeviceをマッチングキーとして使用し、
// IOKitレジストリからディスクの各種プロパティを取得する。
// Uses IOBlockStorageDevice as the matching key to retrieve
// various disk properties from the IOKit registry.
#pragma warning disable CA1806
public static class DiskInfo
{
    /// <summary>
    /// 接続されている全ディスクの情報を取得する。
    /// Retrieves information for all connected disks.
    /// </summary>
    public static IReadOnlyList<IDiskInfo> GetInformation()
    {
        var matching = IOServiceMatching("IOBlockStorageDevice");
        if (matching == IntPtr.Zero)
        {
            return [];
        }

        // IOServiceGetMatchingServicesはmatchingを消費する (CFRelease不要)
        // IOServiceGetMatchingServices consumes matching (no CFRelease needed)
        var iterHandle = 0u;
        if (IOServiceGetMatchingServices(0, matching, ref iterHandle) != KERN_SUCCESS || iterHandle == 0)
        {
            return [];
        }

        using var iter = new IOObj(iterHandle);
        var results = new List<IDiskInfo>();
        var index = 0u;
        uint entryHandle;
        while ((entryHandle = IOIteratorNext(iter)) != 0u)
        {
            using var entry = new IOObj(entryHandle);
            results.Add(ReadDiskEntry(entry, index++));
        }

        return results;
    }

    /// <summary>
    /// 個々のIORegistryエントリからディスク情報を読み取る。
    /// Reads disk information from an individual IORegistry entry.
    /// </summary>
    private static unsafe DiskInfoGeneric ReadDiskEntry(IOObj entry, uint index)
    {
        // IORegistryエントリ名を取得 / Get the IORegistry entry name
        var nameBuf = stackalloc byte[128];
        IORegistryEntryGetName(entry, nameBuf);
        var deviceName = Marshal.PtrToStringUTF8((IntPtr)nameBuf);

        // Device Characteristics辞書からデバイス基本情報を取得
        // Retrieve basic device info from the Device Characteristics dictionary
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

        // Protocol Characteristics辞書からバス情報を取得
        // Retrieve bus information from the Protocol Characteristics dictionary
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

        // TODO 重複チェック、分離
        // IOBlockStorageDriverのStatistics辞書からI/O統計を取得
        // Retrieve I/O statistics from the IOBlockStorageDriver Statistics dictionary
        DiskIOStatistics? ioStats = null;
        using var statsDict = entry.SearchDictionary("Statistics");
        if (statsDict.IsValid)
        {
            ioStats = new DiskIOStatistics
            {
                BytesRead = (ulong)Math.Max(0L, statsDict.GetInt64("Bytes (Read)")),
                BytesWritten = (ulong)Math.Max(0L, statsDict.GetInt64("Bytes (Write)")),
                OperationsRead = (ulong)Math.Max(0L, statsDict.GetInt64("Operations (Read)")),
                OperationsWritten = (ulong)Math.Max(0L, statsDict.GetInt64("Operations (Write)")),
                TotalTimeRead = (ulong)Math.Max(0L, statsDict.GetInt64("Total Time (Read)")),
                TotalTimeWritten = (ulong)Math.Max(0L, statsDict.GetInt64("Total Time (Write)")),
                RetriesRead = (ulong)Math.Max(0L, statsDict.GetInt64("Retries (Read)")),
                RetriesWritten = (ulong)Math.Max(0L, statsDict.GetInt64("Retries (Write)")),
                ErrorsRead = (ulong)Math.Max(0L, statsDict.GetInt64("Errors (Read)")),
                ErrorsWritten = (ulong)Math.Max(0L, statsDict.GetInt64("Errors (Write)")),
                LatencyTimeRead = (ulong)Math.Max(0L, statsDict.GetInt64("Latency Time (Read)")),
                LatencyTimeWritten = (ulong)Math.Max(0L, statsDict.GetInt64("Latency Time (Write)"))
            };
        }

        // 物理ブロックサイズが取得できなかった場合、論理ブロックサイズをフォールバック
        // Fall back to logical block size when physical block size is unavailable
        if (physicalBlockSize <= 0 && logicalBlockSize > 0)
        {
            physicalBlockSize = logicalBlockSize;
        }

        // モデル名の構築 (ベンダ名がある場合は結合)
        // Build the model string (concatenate vendor name if present)
        var model = BuildModelString(modelName, vendorName);

        // バス種別の判定 / Determine the bus type
        var busType = ParseBusType(physInterconnect);

        // SMARTセッションの作成 / Create SMART session
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
                BsdName = bsdName,
                DeviceName = deviceName,
                Model = model,
                SerialNumber = serialNumber?.Trim() ?? string.Empty,
                FirmwareRevision = firmwareRevision?.Trim() ?? string.Empty,
                MediumType = mediumType?.Trim(),
                Removable = removable,
                Ejectable = ejectable,
                PhysicalBlockSize = physicalBlockSize > 0 ? (uint)physicalBlockSize : 0,
                LogicalBlockSize = logicalBlockSize > 0 ? (uint)logicalBlockSize : 0,
                Size = diskSize > 0 ? (ulong)diskSize : 0,
                BusType = busType,
                BusLocation = physInterconnectLocation,
                ContentType = contentType,
                SmartType = smartType,
                Smart = smart,
                IOStatistics = ioStats
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
    // ReSharper disable StringLiteralTypo
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
    // ReSharper restore StringLiteralTypo

    // CFStringをマネージド文字列に変換
    // Converts a CFString to a managed string
    internal static unsafe string? CfStringToManaged(IntPtr cfString)
    {
        var ptr = CFStringGetCStringPtr(cfString, kCFStringEncodingUTF8);
        if (ptr != IntPtr.Zero)
        {
            return Marshal.PtrToStringUTF8(ptr);
        }

        var length = CFStringGetLength(cfString);
        if (length <= 0)
        {
            return string.Empty;
        }

        // CFStringGetCStringPtrが失敗した場合のフォールバック
        // Fallback when CFStringGetCStringPtr fails
        const int stackAllocThreshold = 1024;
        var bufSize = (int)((length * 4) + 1);

        if (bufSize <= stackAllocThreshold)
        {
            var buf = stackalloc byte[bufSize];
            return CFStringGetCString(cfString, buf, bufSize, kCFStringEncodingUTF8)
                ? Marshal.PtrToStringUTF8((IntPtr)buf)
                : null;
        }

        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(bufSize);
        try
        {
            fixed (byte* buf = rented)
            {
                return CFStringGetCString(cfString, buf, bufSize, kCFStringEncodingUTF8)
                    ? Marshal.PtrToStringUTF8((IntPtr)buf)
                    : null;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
#pragma warning restore CA1806
