namespace MacDotNet.Disk;

using System.Text.RegularExpressions;

using static MacDotNet.Disk.NativeMethods;

public static class DiskInfoExtensions
{
    // パーティション情報を取得
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

    private static string GetPartitionPattern(string bsdName) =>
        $"^{Regex.Escape(bsdName)}s\\d+$";

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
    private static ulong GetPartitionSize(string bsdName)
    {
        var matching = IOServiceMatching("IOMedia");
        if (matching == nint.Zero)
        {
            return 0;
        }

        // BSD Nameでマッチング
        var cfBsdName = CFStringCreateWithCString(nint.Zero, bsdName, kCFStringEncodingUTF8);
        if (cfBsdName == nint.Zero)
        {
            return 0;
        }

        var cfKey = CFStringCreateWithCString(nint.Zero, "BSD Name", kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
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
            var sizeKey = CFStringCreateWithCString(nint.Zero, "Size", kCFStringEncodingUTF8);
            if (sizeKey == nint.Zero)
            {
                return 0;
            }

            try
            {
                var val = IORegistryEntryCreateCFProperty(service, sizeKey, nint.Zero, 0);
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
