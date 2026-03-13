namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// CPU コアの種別。Apple Silicon の Efficiency Core / Performance Core に対応。
/// <para>CPU core type. Corresponds to Apple Silicon Efficiency Core / Performance Core.</para>
/// </summary>
public enum CpuCoreType
{
    /// <summary>高効率コア (E-Core)<br/>High-efficiency core (E-Core)</summary>
    Efficiency = 0,

    /// <summary>高性能コア (P-Core)<br/>High-performance core (P-Core)</summary>
    Performance = 1,
}

/// <summary>
/// 個々の CPU コアの実効周波数を保持するクラス。
/// <para>Holds the effective clock frequency for an individual CPU core.</para>
/// </summary>
public sealed class CpuCoreFrequency
{
    /// <summary>コア番号 (コア種別ごとの 0 始まり連番)<br/>Core number (0-based, per core type)</summary>
    public int Number { get; }

    /// <summary>コア種別 (Efficiency / Performance)<br/>Core type (Efficiency / Performance)</summary>
    public CpuCoreType CoreType { get; }

    /// <summary>
    /// 実効周波数 (MHz)。Update() により更新される。
    /// <para>Effective frequency in MHz. Updated by Update().</para>
    /// </summary>
    public double Frequency { get; internal set; }

    internal string ChannelName = string.Empty;
    internal int[] FreqTable = [];
    internal long[] PrevResidencies = [];
    internal long[] CurrResidencies = [];
    internal int ResidencyOffset = -1;

    internal CpuCoreFrequency(int number, CpuCoreType coreType)
    {
        Number = number;
        CoreType = coreType;
    }
}

/// <summary>
/// Apple Silicon CPU の全コアの実効周波数を管理するクラス。
/// Update() のたびに IOReport を Open/Close してサンプリングを行い、
/// 前回サンプルとの差分から実効周波数を算出する。
/// Apple Silicon (ARM64) 以外の環境では Create() が null を返す。
/// <para>
/// Manages the effective clock frequencies for all cores of an Apple Silicon CPU.
/// Each Update() call opens and closes IOReport to take a new sample, then computes
/// effective frequency from the delta against the previous sample.
/// Create() returns null on non-Apple Silicon (non-ARM64) hardware.
/// </para>
/// </summary>
public sealed class CpuFrequency
{
    private readonly int[] eCoreFreqs;
    private readonly int[] pCoreFreqs;
    private readonly CpuCoreFrequency[] eCores;
    private readonly CpuCoreFrequency[] pCores;
    private bool initialized;

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>コアごとの実効周波数リスト。Update() により値が更新される。<br/>Per-core effective frequency list. Updated by Update().</summary>
    public IReadOnlyList<CpuCoreFrequency> Cores { get; }

    /// <summary>E-Core の周波数テーブル (MHz)<br/>E-Core frequency table (MHz)</summary>
    public IReadOnlyList<int> ECoreFrequencyTable => eCoreFreqs;

    /// <summary>P-Core の周波数テーブル (MHz)<br/>P-Core frequency table (MHz)</summary>
    public IReadOnlyList<int> PCoreFrequencyTable => pCoreFreqs;

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private CpuFrequency(string cpuName)
    {
        var tables = GetFrequencyTables(cpuName);
        if (tables is not { } t || (t.eCoreFreqs.Length == 0 && t.pCoreFreqs.Length == 0))
        {
            throw new InvalidOperationException("周波数テーブルを取得できませんでした。Apple Silicon Mac で実行してください。");
        }

        eCoreFreqs = t.eCoreFreqs;
        pCoreFreqs = t.pCoreFreqs;

        var eCoreCount = GetSystemControlInt32("hw.perflevel1.logicalcpu");
        var pCoreCount = GetSystemControlInt32("hw.perflevel0.logicalcpu");

        eCores = new CpuCoreFrequency[eCoreCount];
        pCores = new CpuCoreFrequency[pCoreCount];
        for (var i = 0; i < eCoreCount; i++)
        {
            eCores[i] = new CpuCoreFrequency(i, CpuCoreType.Efficiency);
        }

        for (var i = 0; i < pCoreCount; i++)
        {
            pCores[i] = new CpuCoreFrequency(i, CpuCoreType.Performance);
        }

        var allCores = new CpuCoreFrequency[eCoreCount + pCoreCount];
        eCores.CopyTo(allCores, 0);
        pCores.CopyTo(allCores, eCoreCount);
        Cores = allCores;
    }

    /// <summary>
    /// CpuFrequency インスタンスを生成する。
    /// Apple Silicon (ARM64) 以外の環境、または周波数テーブルの取得に失敗した場合は null を返す。
    /// <para>
    /// Creates a CpuFrequency instance.
    /// Returns null on non-Apple Silicon (non-ARM64) hardware or if the frequency table cannot be retrieved.
    /// </para>
    /// </summary>
    public static CpuFrequency? Create()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            return null;
        }

        var cpuName = GetSystemControlString("machdep.cpu.brand_string") ?? string.Empty;
        try
        {
            return new CpuFrequency(cpuName);
        }
        catch
        {
            return null;
        }
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOReport を Open して各コアの実効周波数を更新し、Close する。
    /// スリープは行わない。呼び出し側が間隔を制御する (例: Thread.Sleep(1000))。
    /// 初回呼び出しは前回サンプルの記録のみを行うため 0 を返す。
    /// <para>
    /// Opens IOReport, updates each core's effective frequency, then closes it.
    /// Does not sleep; the caller controls the sampling interval (e.g. Thread.Sleep(1000)).
    /// The first call only records the initial sample and returns false.
    /// </para>
    /// </summary>
    public bool Update()
    {
        var channels = GetChannels();
        if (channels == IntPtr.Zero)
        {
            return false;
        }

        var subscription = IOReportCreateSubscription(IntPtr.Zero, channels, out var dict, 0, IntPtr.Zero);
        using var subDict = new CFRef(dict);

        if (subscription == IntPtr.Zero)
        {
            CFRelease(channels);
            return false;
        }

        var updated = false;
        using var sample = new CFRef(IOReportCreateSamples(subscription, channels, IntPtr.Zero));
        if (sample.IsValid)
        {
            using var key = CFRef.CreateString("IOReportChannels");
            var items = CFDictionaryGetValue(sample, key);
            if (items != IntPtr.Zero)
            {
                if (!initialized)
                {
                    SetupCoreChannels(items);
                    initialized = true;
                }
                else
                {
                    UpdateFrequencies(items);
                    UpdateAt = DateTime.Now;
                    updated = true;
                }
            }
        }

        CFRelease(subscription);
        CFRelease(channels);
        return updated;
    }

    //--------------------------------------------------------------------------------
    // Setup (first Update call only)
    //--------------------------------------------------------------------------------

    private void SetupCoreChannels(IntPtr items)
    {
        var eCoreNames = new List<string>();
        var pCoreNames = new List<string>();

        var count = CFArrayGetCount(items);
        for (var idx = 0L; idx < count; idx++)
        {
            var item = CFArrayGetValueAtIndex(items, idx);
            if (ToManagedString(IOReportChannelGetSubGroup(item)) != "CPU Core Performance States")
            {
                continue;
            }

            var channelName = ToManagedString(IOReportChannelGetChannelName(item));
            if (channelName == null)
            {
                continue;
            }

            if (channelName.StartsWith("ECPU", StringComparison.Ordinal))
            {
                if (!eCoreNames.Contains(channelName))
                {
                    eCoreNames.Add(channelName);
                }
            }
            else if (channelName.StartsWith("PCPU", StringComparison.Ordinal))
            {
                if (!pCoreNames.Contains(channelName))
                {
                    pCoreNames.Add(channelName);
                }
            }
        }

        eCoreNames.Sort(StringComparer.Ordinal);
        pCoreNames.Sort(StringComparer.Ordinal);

        AssignChannels(eCores, eCoreNames, eCoreFreqs, items, count);
        AssignChannels(pCores, pCoreNames, pCoreFreqs, items, count);
    }

    private static void AssignChannels(
        CpuCoreFrequency[] cores, List<string> channelNames, int[] freqTable,
        IntPtr items, long itemCount)
    {
        for (var ci = 0; ci < channelNames.Count && ci < cores.Length; ci++)
        {
            var core = cores[ci];
            core.ChannelName = channelNames[ci];
            core.FreqTable = freqTable;

            for (var idx = 0L; idx < itemCount; idx++)
            {
                var item = CFArrayGetValueAtIndex(items, idx);
                if (ToManagedString(IOReportChannelGetChannelName(item)) != core.ChannelName)
                {
                    continue;
                }

                var stateCount = IOReportStateGetCount(item);
                core.PrevResidencies = new long[stateCount];
                core.CurrResidencies = new long[stateCount];

                for (var s = 0; s < stateCount; s++)
                {
                    if (core.ResidencyOffset < 0)
                    {
                        var name = ToManagedString(IOReportStateGetNameForIndex(item, s));
                        if (name is not ("IDLE" or "DOWN" or "OFF"))
                        {
                            core.ResidencyOffset = s;
                        }
                    }

                    core.PrevResidencies[s] = IOReportStateGetResidency(item, s);
                }

                break;
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Frequency update (subsequent Update calls)
    //--------------------------------------------------------------------------------

    private void UpdateFrequencies(IntPtr items)
    {
        for (var i = 0; i < eCores.Length; i++)
        {
            eCores[i].Frequency = 0;
        }

        for (var i = 0; i < pCores.Length; i++)
        {
            pCores[i].Frequency = 0;
        }

        var count = CFArrayGetCount(items);
        for (var idx = 0L; idx < count; idx++)
        {
            var item = CFArrayGetValueAtIndex(items, idx);
            var core = FindCore(ToManagedString(IOReportChannelGetChannelName(item)));
            if (core is null || core.CurrResidencies.Length == 0)
            {
                continue;
            }

            var stateCount = IOReportStateGetCount(item);
            for (var s = 0; s < stateCount && s < core.CurrResidencies.Length; s++)
            {
                core.CurrResidencies[s] = IOReportStateGetResidency(item, s);
            }

            core.Frequency = CalculateFrequencies(core.CurrResidencies, core.PrevResidencies, core.FreqTable, core.ResidencyOffset);

            // バッファをスワップ: 今回値が次回の前回値になる / Swap buffers: current becomes previous for the next call
            (core.PrevResidencies, core.CurrResidencies) = (core.CurrResidencies, core.PrevResidencies);
        }
    }

    private CpuCoreFrequency? FindCore(string? channelName)
    {
        if (channelName is null)
        {
            return null;
        }

        for (var i = 0; i < eCores.Length; i++)
        {
            if (eCores[i].ChannelName == channelName)
            {
                return eCores[i];
            }
        }

        for (var i = 0; i < pCores.Length; i++)
        {
            if (pCores[i].ChannelName == channelName)
            {
                return pCores[i];
            }
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Frequency calculation
    //--------------------------------------------------------------------------------

    /// <summary>
    /// 前回・今回のレジデンシー差分から実効周波数 (MHz) を算出する。
    /// 全ステートの差分合計を分母にすることでアイドル時間を反映した実効周波数を得る。
    /// <para>
    /// Computes the effective frequency (MHz) from the delta between the current and previous residency snapshots.
    /// Using the total delta across all states as the denominator incorporates idle time into the result.
    /// </para>
    /// </summary>
    private static double CalculateFrequencies(long[] curr, long[] prev, int[] freqs, int offset)
    {
        if (offset < 0)
        {
            return 0;
        }

        double totalDelta = 0;
        for (var i = 0; i < curr.Length; i++)
        {
            totalDelta += curr[i] - prev[i];
        }

        double avgFreq = 0;
        for (var i = 0; i < freqs.Length; i++)
        {
            var key = i + offset;
            if (key >= curr.Length)
            {
                continue;
            }

            var delta = (double)(curr[key] - prev[key]);
            var percent = totalDelta == 0 ? 0 : delta / totalDelta;
            avgFreq += percent * freqs[i];
        }

        return avgFreq;
    }

    //--------------------------------------------------------------------------------
    // IOReport channel setup
    //--------------------------------------------------------------------------------

    private static IntPtr GetChannels()
    {
        try
        {
            var channelDefs = new[]
            {
                ("CPU Stats", "CPU Complex Performance States"),
                ("CPU Stats", "CPU Core Performance States"),
            };

            var merged = IntPtr.Zero;
            foreach (var (gname, sname) in channelDefs)
            {
                using var g = CFRef.CreateString(gname);
                using var s = CFRef.CreateString(sname);
                var channel = IOReportCopyChannelsInGroup(g, s, 0, 0, 0);
                if (channel == IntPtr.Zero)
                {
                    continue;
                }

                if (merged == IntPtr.Zero)
                {
                    merged = channel;
                }
                else
                {
                    IOReportMergeChannels(merged, channel, IntPtr.Zero);
                    CFRelease(channel);
                }
            }

            if (merged == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var mutableCopy = CFDictionaryCreateMutableCopy(IntPtr.Zero, 0, merged);
            CFRelease(merged);

            if (mutableCopy == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            using var key = CFRef.CreateString("IOReportChannels");
            if (CFDictionaryGetValue(mutableCopy, key) == IntPtr.Zero)
            {
                CFRelease(mutableCopy);
                return IntPtr.Zero;
            }

            return mutableCopy;
        }
        catch (EntryPointNotFoundException)
        {
            return IntPtr.Zero;
        }
    }

    //--------------------------------------------------------------------------------
    // Frequency table (AppleARMIODevice "pmgr")
    //--------------------------------------------------------------------------------

    private static unsafe (int[] eCoreFreqs, int[] pCoreFreqs)? GetFrequencyTables(string cpuName)
    {
        var matching = IOServiceMatching("AppleARMIODevice");
        if (matching == IntPtr.Zero)
        {
            return null;
        }

        if (IOServiceGetMatchingServices(0, matching, out var iterator) != 0)
        {
            return null;
        }

        var isM4OrLater = cpuName.Contains("M4", StringComparison.OrdinalIgnoreCase)
                       || cpuName.Contains("M5", StringComparison.OrdinalIgnoreCase);

        int[] eFreqs = [];
        int[] pFreqs = [];

        try
        {
            uint child;
            while ((child = IOIteratorNext(iterator)) != 0)
            {
                using var entry = new IOObj(child);

                byte* nameBuf = stackalloc byte[128];
                if (IORegistryEntryGetName(entry, nameBuf) != 0)
                {
                    continue;
                }

                if (Marshal.PtrToStringUTF8((IntPtr)nameBuf) != "pmgr")
                {
                    continue;
                }

                if (IORegistryEntryCreateCFProperties(entry, out var propsRef, IntPtr.Zero, 0) != 0)
                {
                    continue;
                }

                using var props = new CFRef(propsRef);

                using var eKey = CFRef.CreateString("voltage-states1-sram");
                var eData = CFDictionaryGetValue(props, eKey);
                if (eData != IntPtr.Zero)
                {
                    eFreqs = ConvertCFDataToFrequencyArray(eData, isM4OrLater);
                }

                using var pKey = CFRef.CreateString("voltage-states5-sram");
                var pData = CFDictionaryGetValue(props, pKey);
                if (pData != IntPtr.Zero)
                {
                    pFreqs = ConvertCFDataToFrequencyArray(pData, isM4OrLater);
                }
            }
        }
        finally
        {
            IOObjectRelease(iterator);
        }

        return (eFreqs, pFreqs);
    }

    /// <summary>
    /// CFData からバイト列を読み取り、8 バイトチャンクごとに周波数 (MHz) へ変換する。
    /// <para>Reads raw bytes from CFData and converts each 8-byte chunk to a frequency value in MHz.</para>
    /// </summary>
    private static int[] ConvertCFDataToFrequencyArray(IntPtr cfData, bool isM4)
    {
        var length = (int)CFDataGetLength(cfData);
        var ptr = CFDataGetBytePtr(cfData);

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);

        var multiplier = isM4 ? 1000u : 1_000_000u;

        var result = new List<int>();
        for (var i = 0; i + 7 < length; i += 8)
        {
            var v = (uint)bytes[i]
                  | ((uint)bytes[i + 1] << 8)
                  | ((uint)bytes[i + 2] << 16)
                  | ((uint)bytes[i + 3] << 24);
            result.Add((int)(v / multiplier));
        }

        return result.ToArray();
    }
}
