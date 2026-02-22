namespace MacDotNet.Disk;

using System.Runtime.InteropServices;

using static MacDotNet.Disk.NativeMethods;

// ディスク情報取得処理
// IOBlockStorageDeviceをマッチングキーとして使用し、
// IOKitレジストリからディスクの各種プロパティを取得する。
#pragma warning disable CA1806
public static class DiskInfo
{
    public static IReadOnlyList<IDiskInfo> GetInformation()
    {
        var matching = IOServiceMatching("IOBlockStorageDevice");
        if (matching == nint.Zero)
        {
            return [];
        }

        // IOServiceGetMatchingServicesはmatchingを消費する (CFRelease不要)
        var iter = nint.Zero;
        if (IOServiceGetMatchingServices(0, matching, ref iter) != KERN_SUCCESS || iter == nint.Zero)
        {
            return [];
        }

        try
        {
            var results = new List<IDiskInfo>();
            uint entry;
            var index = 0u;
            while ((entry = IOIteratorNext(iter)) != 0)
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

    private static unsafe DiskInfoGeneric ReadDiskEntry(uint entry, uint index)
    {
        // IORegistryエントリ名を取得
        var nameBuf = stackalloc byte[128];
        IORegistryEntryGetName(entry, nameBuf);
        var deviceName = Marshal.PtrToStringUTF8((nint)nameBuf);

        // Device Characteristics辞書からデバイス基本情報を取得
        string? modelName = null;
        string? vendorName = null;
        string? serialNumber = null;
        string? firmwareRevision = null;
        string? mediumType = null;

        var devCharDict = GetDictionaryProperty(entry, "Device Characteristics");
        if (devCharDict != nint.Zero)
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
        string? physInterconnect = null;
        string? physInterconnectLocation = null;

        var protoCharDict = GetDictionaryProperty(entry, "Protocol Characteristics");
        if (protoCharDict != nint.Zero)
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
        var bsdName = SearchStringProperty(entry, "BSD Name");
        var diskSize = SearchNumberProperty(entry, "Size");
        var logicalBlockSize = SearchNumberProperty(entry, "Preferred Block Size");
        var physicalBlockSize = SearchNumberProperty(entry, "Physical Block Size");
        var removable = SearchBoolProperty(entry, "Removable");
        var ejectable = SearchBoolProperty(entry, "Ejectable");
        var contentType = SearchStringProperty(entry, "Content");

        // IOBlockStorageDriverのStatistics辞書からI/O統計を取得
        DiskIOStatistics? ioStats = null;
        var statsDict = SearchDictionaryProperty(entry, "Statistics");
        if (statsDict != nint.Zero)
        {
            try
            {
                ioStats = new DiskIOStatistics
                {
                    BytesRead = GetDictNumber(statsDict, "Bytes (Read)"),
                    BytesWritten = GetDictNumber(statsDict, "Bytes (Write)"),
                    OperationsRead = GetDictNumber(statsDict, "Operations (Read)"),
                    OperationsWritten = GetDictNumber(statsDict, "Operations (Write)"),
                    TotalTimeRead = GetDictNumber(statsDict, "Total Time (Read)"),
                    TotalTimeWritten = GetDictNumber(statsDict, "Total Time (Write)"),
                    RetriesRead = GetDictNumber(statsDict, "Retries (Read)"),
                    RetriesWritten = GetDictNumber(statsDict, "Retries (Write)"),
                    ErrorsRead = GetDictNumber(statsDict, "Errors (Read)"),
                    ErrorsWritten = GetDictNumber(statsDict, "Errors (Write)"),
                    LatencyTimeRead = GetDictNumber(statsDict, "Latency Time (Read)"),
                    LatencyTimeWritten = GetDictNumber(statsDict, "Latency Time (Write)")
                };
            }
            finally
            {
                CFRelease(statsDict);
            }
        }

        // 物理ブロックサイズが取得できなかった場合、論理ブロックサイズをフォールバック
        if (physicalBlockSize <= 0 && logicalBlockSize > 0)
        {
            physicalBlockSize = logicalBlockSize;
        }

        // モデル名の構築 (ベンダ名がある場合は結合)
        var model = BuildModelString(modelName, vendorName);

        // バス種別の判定
        var busType = ParseBusType(physInterconnect);

        // SMARTセッションの作成
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
    // IORegistry ヘルパー
    //------------------------------------------------------------------------

    // IORegistryから辞書プロパティを直接取得 (呼び出し元がCFReleaseすること)
    private static nint GetDictionaryProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return nint.Zero;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, nint.Zero, 0);
            if (val == nint.Zero)
            {
                return nint.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return nint.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して文字列プロパティを取得
    private static string? SearchStringProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return null;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
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
    private static long SearchNumberProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return 0;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
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
    private static bool SearchBoolProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return false;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
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
    private static nint SearchDictionaryProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return nint.Zero;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
            {
                return nint.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return nint.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // CFDictionaryから文字列値を取得
    internal static string? GetDictString(nint dict, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return null;
        }

        try
        {
            // CFDictionaryGetValueはGet規則 — 返り値をCFReleaseしてはならない
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == nint.Zero)
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
    internal static long GetDictNumber(nint dict, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return 0;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == nint.Zero)
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
    internal static unsafe string? CfStringToManaged(nint cfString)
    {
        var ptr = CFStringGetCStringPtr(cfString, kCFStringEncodingUTF8);
        if (ptr != nint.Zero)
        {
            return Marshal.PtrToStringUTF8(ptr);
        }

        var length = CFStringGetLength(cfString);
        if (length <= 0)
        {
            return string.Empty;
        }

        // CFStringGetCStringPtrが失敗した場合のフォールバック
        const int stackAllocThreshold = 1024;
        var bufSize = (int)((length * 4) + 1);

        if (bufSize <= stackAllocThreshold)
        {
            var buf = stackalloc byte[bufSize];
            return CFStringGetCString(cfString, buf, bufSize, kCFStringEncodingUTF8)
                ? Marshal.PtrToStringUTF8((nint)buf)
                : null;
        }

        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(bufSize);
        try
        {
            fixed (byte* buf = rented)
            {
                return CFStringGetCString(cfString, buf, bufSize, kCFStringEncodingUTF8)
                    ? Marshal.PtrToStringUTF8((nint)buf)
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
