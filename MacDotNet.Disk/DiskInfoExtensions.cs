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
}
