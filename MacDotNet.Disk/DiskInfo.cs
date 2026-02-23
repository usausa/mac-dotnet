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
        var iter = 0u;
        if (IOServiceGetMatchingServices(0, matching, ref iter) != KERN_SUCCESS || iter == 0)
        {
            return [];
        }

        try
        {
            var results = new List<IDiskInfo>();
            uint entry;
            var index = 0u;
            while ((entry = IOIteratorNext(iter)) != 0u)
            {
                try
                {
                    results.Add(ReadDiskEntry(entry, index++));
                }
                finally
                {
                    IOObjectRelease(entry);
                }
            }

            return results;
        }
        finally
        {
            IOObjectRelease(iter);
        }
    }

    /// <summary>
    /// 個々のIORegistryエントリからディスク情報を読み取る。
    /// Reads disk information from an individual IORegistry entry.
    /// </summary>
    private static unsafe DiskInfoGeneric ReadDiskEntry(uint entry, uint index)
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

        var devCharDict = GetDictionaryProperty(entry, "Device Characteristics");
        if (devCharDict != IntPtr.Zero)
        {
            try
            {
                modelName = GetDictString(devCharDict, "Product Name");
                vendorName = GetDictString(devCharDict, "Vendor Name");
                serialNumber = GetDictString(devCharDict, "Serial Number");
                firmwareRevision = GetDictString(devCharDict, "Product Revision Level");
                mediumType = GetDictString(devCharDict, "Medium Type");
            }
            finally
            {
                CFRelease(devCharDict);
            }
        }

        // Protocol Characteristics辞書からバス情報を取得
        // Retrieve bus information from the Protocol Characteristics dictionary
        string? physInterconnect = null;
        string? physInterconnectLocation = null;

        var protoCharDict = GetDictionaryProperty(entry, "Protocol Characteristics");
        if (protoCharDict != IntPtr.Zero)
        {
            try
            {
                physInterconnect = GetDictString(protoCharDict, "Physical Interconnect");
                physInterconnectLocation = GetDictString(protoCharDict, "Physical Interconnect Location");
            }
            finally
            {
                CFRelease(protoCharDict);
            }
        }

        // 子エントリを再帰検索してIOMedia/IOBlockStorageDriverのプロパティを取得
        // Recursively search child entries to obtain IOMedia / IOBlockStorageDriver properties
        var bsdName = SearchStringProperty(entry, "BSD Name");
        var diskSize = SearchNumberProperty(entry, "Size");
        var logicalBlockSize = SearchNumberProperty(entry, "Preferred Block Size");
        var physicalBlockSize = SearchNumberProperty(entry, "Physical Block Size");
        var removable = SearchBoolProperty(entry, "Removable");
        var ejectable = SearchBoolProperty(entry, "Ejectable");
        var contentType = SearchStringProperty(entry, "Content");

        // IOBlockStorageDriverのStatistics辞書からI/O統計を取得
        // Retrieve I/O statistics from the IOBlockStorageDriver Statistics dictionary
        DiskIOStatistics? ioStats = null;
        var statsDict = SearchDictionaryProperty(entry, "Statistics");
        if (statsDict != IntPtr.Zero)
        {
            try
            {
                ioStats = new DiskIOStatistics
                {
                    BytesRead = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Bytes (Read)")),
                    BytesWritten = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Bytes (Write)")),
                    OperationsRead = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Operations (Read)")),
                    OperationsWritten = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Operations (Write)")),
                    TotalTimeRead = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Total Time (Read)")),
                    TotalTimeWritten = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Total Time (Write)")),
                    RetriesRead = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Retries (Read)")),
                    RetriesWritten = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Retries (Write)")),
                    ErrorsRead = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Errors (Read)")),
                    ErrorsWritten = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Errors (Write)")),
                    LatencyTimeRead = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Latency Time (Read)")),
                    LatencyTimeWritten = (ulong)Math.Max(0L, GetDictNumber(statsDict, "Latency Time (Write)"))
                };
            }
            finally
            {
                CFRelease(statsDict);
            }
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

    //------------------------------------------------------------------------
    // IORegistry ヘルパー / IORegistry Helpers
    //------------------------------------------------------------------------

    // IORegistryから辞書プロパティを直接取得 (呼び出し元がCFReleaseすること)
    // Directly retrieves a dictionary property from the IORegistry (caller must CFRelease)
    private static IntPtr GetDictionaryProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return IntPtr.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して文字列プロパティを取得
    // Recursively searches child entries to retrieve a string property
    private static string? SearchStringProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively);
            if (val == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return CFGetTypeID(val) == CFStringGetTypeID() ? CfStringToManaged(val) : null;
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して数値プロパティを取得
    // Recursively searches child entries to retrieve a numeric property
    private static long SearchNumberProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                if (CFGetTypeID(val) != CFNumberGetTypeID())
                {
                    return 0;
                }

                long result = 0;
                CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
                return result;
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して真偽値プロパティを取得
    // Recursively searches child entries to retrieve a boolean property
    private static bool SearchBoolProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively);
            if (val == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return CFGetTypeID(val) == CFBooleanGetTypeID() && CFBooleanGetValue(val);
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して辞書プロパティを取得 (呼び出し元がCFReleaseすること)
    // Recursively searches child entries to retrieve a dictionary property (caller must CFRelease)
    private static IntPtr SearchDictionaryProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively);
            if (val == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return IntPtr.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // CFDictionaryから文字列値を取得
    // Retrieves a string value from a CFDictionary
    internal static string? GetDictString(IntPtr dict, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            // CFDictionaryGetValueはGet規則 — 返り値をCFReleaseしてはならない
            // CFDictionaryGetValue follows the Get Rule — the returned value must NOT be CFReleased
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == IntPtr.Zero)
            {
                return null;
            }

            return CFGetTypeID(val) == CFStringGetTypeID() ? CfStringToManaged(val) : null;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // CFDictionaryから数値を取得
    // Retrieves a numeric value from a CFDictionary
    internal static long GetDictNumber(IntPtr dict, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            if (CFGetTypeID(val) != CFNumberGetTypeID())
            {
                return 0;
            }

            long result = 0;
            CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
            return result;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

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
