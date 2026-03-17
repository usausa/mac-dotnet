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
                        : 0
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

        using var iterObj = new IOObj(iter);
        uint entryHandle;
        while ((entryHandle = IOIteratorNext(iterObj)) != 0)
        {
            using var entry = new IOObj(entryHandle);

            // Whole=true のエントリのみ対象
            if (!entry.GetBoolean("Whole"))
            {
                continue;
            }

            // BSD 名を取得
            var bsdName = entry.GetString("BSD Name");
            if (bsdName is null || result.Contains(bsdName))
            {
                continue;
            }

            // 親が IOBlockStorageDriver なら物理ディスク → スキップ
            if (IORegistryEntryGetParentEntry(entry, kIOServicePlane, out var parentHandle) != KERN_SUCCESS || parentHandle == 0)
            {
                continue;
            }

            bool isPhysical;
            using (var parent = new IOObj(parentHandle))
            {
                isPhysical = parent.GetClassName() == "IOBlockStorageDriver";
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
    }

    /// <summary>
    /// IOKit の親チェーンを辿り、physicalDiskBsdName を持つエントリが存在するか確認する。
    /// </summary>
    private static bool IsUnderPhysicalDisk(IOObj entry, string physicalDiskBsdName)
    {
        var currentHandle = (uint)entry;
        var owned = IOObj.Zero;

        for (var depth = 0; depth < 20; depth++)
        {
            if (IORegistryEntryGetParentEntry(currentHandle, kIOServicePlane, out var parentHandle) != KERN_SUCCESS || parentHandle == 0)
            {
                break;
            }

            owned.Dispose();
            owned = new IOObj(parentHandle);
            currentHandle = parentHandle;

            if (owned.GetString("BSD Name") == physicalDiskBsdName)
            {
                owned.Dispose();
                return true;
            }

            // IOBlockStorageDevice まで到達したら打ち切る
            if (owned.GetClassName() == "IOBlockStorageDevice")
            {
                break;
            }
        }

        owned.Dispose();
        return false;
    }

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
        using var cfBsdName = CFRef.CreateString(bsdName);
        using var cfKey = CFRef.CreateString("BSD Name");
        if (!cfBsdName.IsValid || !cfKey.IsValid)
        {
            // IOServiceGetMatchingServiceに渡す前に早期リターンするため手動解放
            // Manual release because we return before passing to IOServiceGetMatchingService
            CFRelease(matching);
            return 0;
        }

        CoreFoundationSetDictionaryValue(matching, cfKey, cfBsdName);

        // IOServiceGetMatchingServiceはmatchingを消費する (CFRelease不要)
        // IOServiceGetMatchingService consumes matching (no CFRelease needed)
        var serviceHandle = IOServiceGetMatchingService(0, matching);
        if (serviceHandle == 0)
        {
            return 0;
        }

        using var service = new IOObj(serviceHandle);
        var size = service.GetInt64("Size");
        return size > 0 ? (ulong)size : 0;
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
}
