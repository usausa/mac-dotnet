using System.Runtime.InteropServices;
using static CpuFrequencySample.NativeBindings;

namespace CpuFrequencySample;

/// <summary>
/// AppleARMIODevice の "pmgr" から E-Core / P-Core の周波数テーブルを取得する。
/// Swift版 SystemKit.swift の getFrequencies() に対応。
/// </summary>
static class FrequencyTableReader
{
    /// <summary>
    /// voltage-states*-sram から周波数テーブル (MHz) を読み取る。
    /// Swift版: SystemKit.getFrequencies(cpuName:)
    /// </summary>
    public static (int[] eCoreFreqs, int[] pCoreFreqs)? GetFrequencyTables(string cpuName)
    {
        var matching = IOServiceMatching("AppleARMIODevice");
        if (matching == IntPtr.Zero) return null;

        if (IOServiceGetMatchingServices(kIOMasterPortDefault, matching, out uint iterator) != 0)
            return null;

        // Swift版と同じ: M4/M5 チップは divisor が 1000、それ以外は 1,000,000
        var isM4OrLater = cpuName.Contains("M4", StringComparison.OrdinalIgnoreCase)
                       || cpuName.Contains("M5", StringComparison.OrdinalIgnoreCase);

        int[] eFreqs = [];
        int[] pFreqs = [];

        uint child;
        while ((child = IOIteratorNext(iterator)) != 0)
        {
            try
            {
                // Swift版: guard let name = getIOName(child), name == "pmgr"
                var nameBuffer = Marshal.AllocHGlobal(128);
                try
                {
                    if (IORegistryEntryGetName(child, nameBuffer) != 0) continue;
                    var name = Marshal.PtrToStringUTF8(nameBuffer);
                    if (name != "pmgr") continue;
                }
                finally { Marshal.FreeHGlobal(nameBuffer); }

                // Swift版: let props = getIOProperties(child)
                if (IORegistryEntryCreateCFProperties(child, out IntPtr propsRef, IntPtr.Zero, 0) != 0)
                    continue;

                // Swift版: props.value(forKey: "voltage-states1-sram") → E-Core 周波数
                var eKey = CreateCFString("voltage-states1-sram");
                if (CFDictionaryGetValueIfPresent(propsRef, eKey, out IntPtr eData))
                    eFreqs = ConvertCFDataToFrequencyArray(eData, isM4OrLater);
                CFRelease(eKey);

                // Swift版: props.value(forKey: "voltage-states5-sram") → P-Core 周波数
                var pKey = CreateCFString("voltage-states5-sram");
                if (CFDictionaryGetValueIfPresent(propsRef, pKey, out IntPtr pData))
                    pFreqs = ConvertCFDataToFrequencyArray(pData, isM4OrLater);
                CFRelease(pKey);

                CFRelease(propsRef);
            }
            finally { IOObjectRelease(child); }
        }

        IOObjectRelease(iterator);
        return (eFreqs, pFreqs);
    }

    /// <summary>
    /// CFData からバイト列を読み取り、8バイトチャンクごとに周波数 (MHz) へ変換する。
    /// Swift版: helpers.swift の convertCFDataToArr()
    /// </summary>
    private static int[] ConvertCFDataToFrequencyArray(IntPtr cfData, bool isM4)
    {
        var length = (int)CFDataGetLength(cfData);
        var ptr = CFDataGetBytePtr(cfData);

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);

        // Swift版と同じ: M4+ は 1000, それ以外は 1,000,000
        uint multiplier = isM4 ? 1000u : 1_000_000u;

        var result = new List<int>();
        for (int i = 0; i + 7 < length; i += 8)
        {
            // Swift版: UInt32(chunk[0]) | UInt32(chunk[1]) << 8 | ...
            uint v = (uint)bytes[i]
                   | ((uint)bytes[i + 1] << 8)
                   | ((uint)bytes[i + 2] << 16)
                   | ((uint)bytes[i + 3] << 24);
            result.Add((int)(v / multiplier));
        }
        return result.ToArray();
    }
}
