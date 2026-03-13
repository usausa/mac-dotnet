namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class CpuCoreFrequency
{
    public int Number { get; }

    public CpuCoreType CoreType { get; }

    public double Frequency { get; internal set; }

#pragma warning disable SA1401
    internal string ChannelName = string.Empty;

    internal int[] FreqTable = [];

    internal long[] PrevResidencies = [];

    internal long[] CurrResidencies = [];

    internal int ResidencyOffset = -1;
#pragma warning restore SA1401

    internal CpuCoreFrequency(int number, CpuCoreType coreType)
    {
        Number = number;
        CoreType = coreType;
    }
}

public sealed class CpuFrequency
{
    private readonly int[] eCoreFreqs;
    private readonly int[] pCoreFreqs;
    private readonly CpuCoreFrequency[] eCores;
    private readonly CpuCoreFrequency[] pCores;

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>コアごとの実効周波数リスト。Update() により値が更新される。<br/>Per-core effective frequency list. Updated by Update().</summary>
    public IReadOnlyList<CpuCoreFrequency> Cores { get; }

    /// <summary>E-Core の周波数テーブル (MHz)<br/>E-Core frequency table (MHz)</summary>
    public IReadOnlyList<int> ECoreFrequencyTable => eCoreFreqs;

    /// <summary>P-Core の周波数テーブル (MHz)<br/>P-Core frequency table (MHz)</summary>
    public IReadOnlyList<int> PCoreFrequencyTable => pCoreFreqs;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal CpuFrequency()
    {
        // 周波数テーブル取得
        var cpuName = GetSystemControlString("machdep.cpu.brand_string") ?? string.Empty;
        var tables = GetFrequencyTables(cpuName);
        if (tables is not { } t || (t.eCoreFreqs.Length == 0 && t.pCoreFreqs.Length == 0))
        {
            throw new InvalidOperationException("周波数テーブルを取得できませんでした。Apple Silicon Mac で実行してください。");
        }

        eCoreFreqs = t.eCoreFreqs;
        pCoreFreqs = t.pCoreFreqs;

        // コアオブジェクト生成
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

        // IOReport を開いてチャンネルを検出し、ベースラインレジデンシーを記録する
        using var channels = new CFRef(GetChannels());
        if (channels.IsValid)
        {
            using var subscription = new CFRef(IOReportCreateSubscription(IntPtr.Zero, channels, out var dict, 0, IntPtr.Zero));
            using var subDict = new CFRef(dict);
            if (subscription.IsValid)
            {
                using var sample = new CFRef(IOReportCreateSamples(subscription, channels, IntPtr.Zero));
                if (sample.IsValid)
                {
                    using var key = CFRef.CreateString("IOReportChannels");
                    var items = CFDictionaryGetValue(sample, key);
                    if (items != IntPtr.Zero)
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
                }
            }
        }
    }

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

        using var iter = new IOObj(iterator);

        var isM4OrLater = cpuName.Contains("M4", StringComparison.OrdinalIgnoreCase)
                       || cpuName.Contains("M5", StringComparison.OrdinalIgnoreCase);

        int[] eFreqs = [];
        int[] pFreqs = [];

        uint child;
        while ((child = IOIteratorNext(iter)) != 0)
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
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOReport を Open して各コアの実効周波数を更新し、Close する。
    /// スリープは行わない。呼び出し側が間隔を制御する (例: Thread.Sleep(1000))。
    /// <para>
    /// Opens IOReport, updates each core's effective frequency, then closes it.
    /// Does not sleep; the caller controls the sampling interval (e.g. Thread.Sleep(1000)).
    /// </para>
    /// </summary>
    public bool Update()
    {
        using var channels = new CFRef(GetChannels());
        if (!channels.IsValid)
        {
            return false;
        }

        using var subscription = new CFRef(IOReportCreateSubscription(IntPtr.Zero, channels, out var dict, 0, IntPtr.Zero));
        using var subDict = new CFRef(dict);

        if (!subscription.IsValid)
        {
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
                UpdateFrequencies(items);
                UpdateAt = DateTime.Now;
                updated = true;
            }
        }

        return updated;
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

    private static IntPtr GetChannels()
    {
        try
        {
            using var group = CFRef.CreateString("CPU Stats");
            using var subGroup = CFRef.CreateString("CPU Core Performance States");
            using var channel = new CFRef(IOReportCopyChannelsInGroup(group, subGroup, 0, 0, 0));
            if (!channel.IsValid)
            {
                return IntPtr.Zero;
            }

            // IOReportCreateSubscription には CFMutableDictionary が必要なため mutable コピーを作成する
            var mutableCopy = CFDictionaryCreateMutableCopy(IntPtr.Zero, 0, channel);
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
}
