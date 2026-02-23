namespace MacDotNet.Disk;

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
