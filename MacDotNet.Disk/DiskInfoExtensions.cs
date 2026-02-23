namespace MacDotNet.Disk;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using static MacDotNet.Disk.NativeMethods;

/// <summary>
/// ディスク情報の拡張メソッド。
/// Extension methods for disk information.
/// </summary>
public static class DiskInfoExtensions
{
    /// <summary>
    /// パーティション情報を取得する。
    /// Retrieves partition information for the specified disk.
    /// </summary>
    public static IEnumerable<PartitionInfo> GetPartitions(this IDiskInfo disk)
    {
        ArgumentNullException.ThrowIfNull(disk);

        if (disk.BsdName is null)
        {
            yield break;
        }

        var pattern = GetPartitionPattern(disk.BsdName);

        var index = 0u;
        foreach (var name in EnumerateBsdDevices(pattern))
        {
            var devicePath = $"/dev/{name}";

            // IOKitからパーティションサイズを取得
            // Retrieve partition size from IOKit
            var size = GetPartitionSize(name);

            yield return new PartitionInfo
            {
                Index = index++,
                DeviceName = devicePath,
                Name = name,
                Size = size
            };
        }
    }

    // BSD名からパーティション検索用の正規表現パターンを生成する
    // Generates a regex pattern for partition search from a BSD name
    private static string GetPartitionPattern(string bsdName) =>
        $"^{Regex.Escape(bsdName)}s\\d+$";

    // /dev配下のパターンに一致するBSDデバイスを列挙する
    // Enumerates BSD devices under /dev matching the given pattern
    private static IEnumerable<string> EnumerateBsdDevices(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled);

        if (!Directory.Exists("/dev"))
        {
            yield break;
        }

        var entries = Directory.GetFiles("/dev")
            .Select(Path.GetFileName)
            .Where(n => n is not null && regex.IsMatch(n))
            .OrderBy(static n => n, StringComparer.Ordinal);

        foreach (var name in entries)
        {
            yield return name!;
        }
    }

    //------------------------------------------------------------------------
    // GetVolumes
    //------------------------------------------------------------------------

    /// <summary>
    /// この物理ディスクに関連付けられたマウント済みボリュームを返す。
    /// APFS コンテナ経由のボリューム (例: disk3s1s1) も解決して含める。
    /// BsdName が null の場合は空のシーケンスを返す。
    /// <para>
    /// Returns mounted volumes associated with this physical disk.
    /// Also resolves volumes under APFS containers (e.g. disk3s1s1).
    /// Returns an empty sequence when BsdName is null.
    /// </para>
    /// </summary>
    public static unsafe IEnumerable<VolumeInfo> GetVolumes(this IDiskInfo disk)
    {
        ArgumentNullException.ThrowIfNull(disk);

        if (disk.BsdName is null)
        {
            return [];
        }

        // Step 1: 物理ディスク自身と、その上にある APFS コンテナ (合成ディスク) を収集
        var targetDisks = new HashSet<string>(StringComparer.Ordinal) { disk.BsdName };
        CollectApfsContainerDisks(disk.BsdName, targetDisks);

        // Step 2: statfs で全マウント済み FS を取得して対象ディスクのものだけを返す
        var count = getfsstat(null, 0, MNT_NOWAIT);
        if (count <= 0)
        {
            return [];
        }

        var buffer = new statfs_disk[count];
        var result = new List<VolumeInfo>();

        fixed (statfs_disk* ptr = buffer)
        {
            var bufsize = count * sizeof(statfs_disk);
            var actual = getfsstat(ptr, bufsize, MNT_NOWAIT);
            if (actual <= 0)
            {
                return [];
            }

            var limit = Math.Min(actual, count);
            for (var i = 0; i < limit; i++)
            {
                // ローカル FS のみ対象
                if ((ptr[i].f_flags & MNT_LOCAL) == 0)
                {
                    continue;
                }

                var deviceName = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntfromname) ?? string.Empty;

                // /dev/diskXsY... からベースディスク名を抽出
                if (!deviceName.StartsWith("/dev/", StringComparison.Ordinal))
                {
                    continue;
                }

                var bsdDevice = deviceName["/dev/".Length..];
                var baseDisk = ExtractBaseDiskName(bsdDevice);
                if (baseDisk is null || !targetDisks.Contains(baseDisk))
                {
                    continue;
                }

                var totalBlocks = ptr[i].f_blocks;
                var blockSize = ptr[i].f_bsize;

                result.Add(new VolumeInfo
                {
                    MountPoint = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntonname) ?? string.Empty,
                    TypeName = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_fstypename) ?? string.Empty,
                    DeviceName = deviceName,
                    IsReadOnly = (ptr[i].f_flags & MNT_RDONLY) != 0,
                    TotalSize = totalBlocks * blockSize,
                    FreeSize = ptr[i].f_bfree * blockSize,
                    AvailableSize = ptr[i].f_bavail * blockSize,
                    UsagePercent = totalBlocks > 0
                        ? (double)(totalBlocks - ptr[i].f_bavail) / totalBlocks
                        : 0,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// physicalDiskBsdName を物理ルートとして、その配下にある APFS 合成ディスク (disk3 等)
    /// の BSD 名を result に追加する。
    /// </summary>
    private static void CollectApfsContainerDisks(string physicalDiskBsdName, HashSet<string> result)
    {
        var iter = 0u;
        if (IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), ref iter) != KERN_SUCCESS || iter == 0)
        {
            return;
        }

        try
        {
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    // Whole=true のエントリのみ対象
                    var wholePropKey = CFStringCreateWithCString(IntPtr.Zero, "Whole", kCFStringEncodingUTF8);
                    if (wholePropKey == IntPtr.Zero)
                    {
                        continue;
                    }

                    bool isWhole;
                    try
                    {
                        var wholeProp = IORegistryEntryCreateCFProperty(entry, wholePropKey, IntPtr.Zero, 0);
                        if (wholeProp == IntPtr.Zero)
                        {
                            continue;
                        }

                        isWhole = CFBooleanGetValue(wholeProp);
                        CFRelease(wholeProp);
                    }
                    finally
                    {
                        CFRelease(wholePropKey);
                    }

                    if (!isWhole)
                    {
                        continue;
                    }

                    // BSD 名を取得
                    var bsdName = GetBsdNameFromEntry(entry);
                    if (bsdName is null || result.Contains(bsdName))
                    {
                        continue;
                    }

                    // 親が IOBlockStorageDriver なら物理ディスク → スキップ
                    if (IORegistryEntryGetParentEntry(entry, kIOServicePlane, out var parent) != KERN_SUCCESS || parent == 0)
                    {
                        continue;
                    }

                    bool isPhysical;
                    try
                    {
                        isPhysical = GetIokitClassName(parent) == "IOBlockStorageDriver";
                    }
                    finally
                    {
                        _ = IOObjectRelease(parent);
                    }

                    if (isPhysical)
                    {
                        continue;
                    }

                    // 合成ディスク: 親チェーンに物理ディスクの BSD 名があるか確認
                    if (IsUnderPhysicalDisk(entry, physicalDiskBsdName))
                    {
                        result.Add(bsdName);
                    }
                }
                finally
                {
                    _ = IOObjectRelease(entry);
                }
            }
        }
        finally
        {
            _ = IOObjectRelease(iter);
        }
    }

    /// <summary>
    /// IOKit の親チェーンを辿り、physicalDiskBsdName を持つエントリが存在するか確認する。
    /// </summary>
    private static bool IsUnderPhysicalDisk(uint entry, string physicalDiskBsdName)
    {
        var current = entry;
        var shouldRelease = false;

        for (var depth = 0; depth < 20; depth++)
        {
            if (IORegistryEntryGetParentEntry(current, kIOServicePlane, out var parent) != KERN_SUCCESS || parent == 0)
            {
                break;
            }

            if (shouldRelease)
            {
                _ = IOObjectRelease(current);
            }

            current = parent;
            shouldRelease = true;

            var bsdName = GetBsdNameFromEntry(current);
            if (bsdName == physicalDiskBsdName)
            {
                _ = IOObjectRelease(current);
                return true;
            }

            // IOBlockStorageDevice まで到達したら打ち切る
            if (GetIokitClassName(current) == "IOBlockStorageDevice")
            {
                break;
            }
        }

        if (shouldRelease)
        {
            _ = IOObjectRelease(current);
        }

        return false;
    }

    /// <summary>IOKit エントリから BSD 名を取得する。</summary>
    private static string? GetBsdNameFromEntry(uint entry)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, "BSD Name", kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return CFGetTypeID(val) == CFStringGetTypeID() ? DiskInfo.CfStringToManaged(val) : null;
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

    /// <summary>IOKit オブジェクトのクラス名を取得する。</summary>
    private static unsafe string? GetIokitClassName(uint @object)
    {
        const int nameLen = 128;
        byte* buf = stackalloc byte[nameLen];
        return IOObjectGetClass(@object, buf) == KERN_SUCCESS
            ? Marshal.PtrToStringUTF8((IntPtr)buf)
            : null;
    }

    /// <summary>"diskNsX..." → "diskN" のようにベースのディスク番号を抽出する。</summary>
    private static string? ExtractBaseDiskName(string bsdDevice)
    {
        const string prefix = "disk";
        if (!bsdDevice.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var span = bsdDevice.AsSpan(prefix.Length);
        var digitLen = 0;
        while (digitLen < span.Length && char.IsAsciiDigit(span[digitLen]))
        {
            digitLen++;
        }

        return digitLen > 0 ? string.Concat(prefix, span[..digitLen]) : null;
    }

    //------------------------------------------------------------------------
    // GetSmartHealth
    //------------------------------------------------------------------------

    /// <summary>
    /// SMART データから総合的なディスク健全性を判定して返す。
    /// Smart.Update() が未実行の場合は自動的に呼び出す。
    /// SMART 非対応の場合は <see cref="SmartHealthStatus.Unknown"/> を返す。
    /// <para>
    /// Evaluates overall disk health from SMART data.
    /// Calls Smart.Update() automatically if not yet called.
    /// Returns <see cref="SmartHealthStatus.Unknown"/> when SMART is unsupported.
    /// </para>
    /// </summary>
    public static SmartHealthStatus GetSmartHealth(this IDiskInfo disk)
    {
        ArgumentNullException.ThrowIfNull(disk);

        if (!disk.Smart.LastUpdate && !disk.Smart.Update())
        {
            return SmartHealthStatus.Unknown;
        }

        return disk.SmartType switch
        {
            SmartType.Nvme when disk.Smart is ISmartNvme nvme => EvaluateNvmeHealth(nvme),
            SmartType.Generic when disk.Smart is ISmartGeneric ata => EvaluateAtaHealth(ata),
            _ => SmartHealthStatus.Unknown,
        };
    }

    private static SmartHealthStatus EvaluateNvmeHealth(ISmartNvme nvme)
    {
        // Critical: コントローラが重大警告を発している / spare 不足
        if (nvme.CriticalWarning != 0)
        {
            return SmartHealthStatus.Critical;
        }

        if (nvme.AvailableSpare < nvme.AvailableSpareThreshold)
        {
            return SmartHealthStatus.Critical;
        }

        if (nvme.MediaErrors > 0)
        {
            return SmartHealthStatus.Critical;
        }

        // Warning: 使用率が高い / エラーログあり
        if (nvme.PercentageUsed >= 90)
        {
            return SmartHealthStatus.Warning;
        }

        if (nvme.ErrorInfoLogEntries > 0)
        {
            return SmartHealthStatus.Warning;
        }

        return SmartHealthStatus.Healthy;
    }

    private static SmartHealthStatus EvaluateAtaHealth(ISmartGeneric ata)
    {
        // Critical: 回復不能エラー
        var uncorrectable = ata.GetAttribute(SmartId.UncorrectableSectorCount);
        if (uncorrectable is { RawValue: > 0 })
        {
            return SmartHealthStatus.Critical;
        }

        var reportedUncorrectable = ata.GetAttribute(SmartId.ReportedUncorrectableErrors);
        if (reportedUncorrectable is { RawValue: > 0 })
        {
            return SmartHealthStatus.Critical;
        }

        // Warning: 再割り当て済みセクタ・保留セクタ・再割り当てイベント
        var reallocated = ata.GetAttribute(SmartId.ReallocatedSectorCount);
        if (reallocated is { RawValue: > 0 })
        {
            return SmartHealthStatus.Warning;
        }

        var pending = ata.GetAttribute(SmartId.CurrentPendingSectorCount);
        if (pending is { RawValue: > 0 })
        {
            return SmartHealthStatus.Warning;
        }

        var reallocEvents = ata.GetAttribute(SmartId.ReallocationEventCount);
        if (reallocEvents is { RawValue: > 0 })
        {
            return SmartHealthStatus.Warning;
        }

        return SmartHealthStatus.Healthy;
    }

    //------------------------------------------------------------------------
    // GetPartitionSize (既存)
    //------------------------------------------------------------------------

    // IOKitを使用してパーティションサイズを取得
    // Retrieves partition size using IOKit
    private static ulong GetPartitionSize(string bsdName)
    {
        var matching = IOServiceMatching("IOMedia");
        if (matching == IntPtr.Zero)
        {
            return 0;
        }

        // BSD Nameでマッチング / Match by BSD Name
        var cfBsdName = CFStringCreateWithCString(IntPtr.Zero, bsdName, kCFStringEncodingUTF8);
        if (cfBsdName == IntPtr.Zero)
        {
            return 0;
        }

        var cfKey = CFStringCreateWithCString(IntPtr.Zero, "BSD Name", kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            CFRelease(cfBsdName);
            return 0;
        }

        CoreFoundationSetDictionaryValue(matching, cfKey, cfBsdName);
        CFRelease(cfKey);
        CFRelease(cfBsdName);

        var service = IOServiceGetMatchingService(0, matching);
        if (service == 0)
        {
            return 0;
        }

        try
        {
            var sizeKey = CFStringCreateWithCString(IntPtr.Zero, "Size", kCFStringEncodingUTF8);
            if (sizeKey == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                var val = IORegistryEntryCreateCFProperty(service, sizeKey, IntPtr.Zero, 0);
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
                    return result > 0 ? (ulong)result : 0;
                }
                finally
                {
                    CFRelease(val);
                }
            }
            finally
            {
                CFRelease(sizeKey);
            }
        }
        finally
        {
            _ = IOObjectRelease(service);
        }
    }
}
