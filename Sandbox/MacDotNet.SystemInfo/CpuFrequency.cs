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
    private readonly List<CpuCoreFrequency> eCores = [];
    private readonly List<CpuCoreFrequency> pCores = [];
    private readonly List<CpuCoreFrequency> cores = [];

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>コアごとの実効周波数リスト。Update() により値が更新される。<br/>Per-core effective frequency list. Updated by Update().</summary>
    public IReadOnlyList<CpuCoreFrequency> Cores => cores;

    /// <summary>E-Core の最大周波数 (MHz)<br/>E-Core maximum frequency (MHz)</summary>
    public int MaxECoreFrequency => eCoreFreqs.Length > 0 ? eCoreFreqs[^1] : 0;

    /// <summary>P-Core の最大周波数 (MHz)<br/>P-Core maximum frequency (MHz)</summary>
    public int MaxPCoreFrequency => pCoreFreqs.Length > 0 ? pCoreFreqs[^1] : 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal unsafe CpuFrequency()
    {
        var cpuName = GetSystemControlString("machdep.cpu.brand_string") ?? string.Empty;
        var isM4OrLater = cpuName.Contains("M4", StringComparison.OrdinalIgnoreCase)
                       || cpuName.Contains("M5", StringComparison.OrdinalIgnoreCase);

        eCoreFreqs = [];
        pCoreFreqs = [];

        var matching = IOServiceMatching("AppleARMIODevice");
        if (matching != IntPtr.Zero && IOServiceGetMatchingServices(0, matching, out var iterator) == 0)
        {
            using var iter = new IOObj(iterator);

            uint child;
            while ((child = IOIteratorNext(iter)) != 0)
            {
                using var entry = new IOObj(child);

                byte* nameBuf = stackalloc byte[128];
                if (IORegistryEntryGetName(entry, nameBuf) != 0)
                    continue;
                if (Marshal.PtrToStringUTF8((IntPtr)nameBuf) != "pmgr")
                    continue;
                if (IORegistryEntryCreateCFProperties(entry, out var propsRef, IntPtr.Zero, 0) != 0)
                    continue;

                using var props = new CFRef(propsRef);

                using var eKey = CFRef.CreateString("voltage-states1-sram");
                var eData = CFDictionaryGetValue(props, eKey);
                if (eData != IntPtr.Zero)
                    eCoreFreqs = ConvertCFDataToFrequencyArray(eData, isM4OrLater);

                using var pKey = CFRef.CreateString("voltage-states5-sram");
                var pData = CFDictionaryGetValue(props, pKey);
                if (pData != IntPtr.Zero)
                    pCoreFreqs = ConvertCFDataToFrequencyArray(pData, isM4OrLater);
            }
        }

        Update();
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
            return false;

        using var subscription = new CFRef(IOReportCreateSubscription(IntPtr.Zero, channels, out var dict, 0, IntPtr.Zero));
        using var subDict = new CFRef(dict);
        if (!subscription.IsValid)
            return false;

        using var sample = new CFRef(IOReportCreateSamples(subscription, channels, IntPtr.Zero));
        if (!sample.IsValid)
            return false;

        using var key = CFRef.CreateString("IOReportChannels");
        var items = CFDictionaryGetValue(sample, key);
        if (items == IntPtr.Zero)
            return false;

        for (var i = 0; i < cores.Count; i++)
            cores[i].Frequency = 0;

        var count = CFArrayGetCount(items);
        for (var idx = 0L; idx < count; idx++)
        {
            var item = CFArrayGetValueAtIndex(items, idx);
            var channelName = ToManagedString(IOReportChannelGetChannelName(item));
            if (channelName is null)
                continue;

            var isECore = channelName.StartsWith("ECPU", StringComparison.Ordinal);
            var core = FindCore(channelName, isECore);
            if (core is null)
            {
                // 初回: チャンネルに対応するコアを新規作成し PrevResidencies にベースラインを記録する
                var coreType = isECore ? CpuCoreType.Efficiency : CpuCoreType.Performance;
                core = new CpuCoreFrequency(isECore ? eCores.Count : pCores.Count, coreType);
                core.ChannelName = channelName;
                core.FreqTable = isECore ? eCoreFreqs : pCoreFreqs;

                var newStateCount = IOReportStateGetCount(item);
                core.PrevResidencies = new long[newStateCount];
                core.CurrResidencies = new long[newStateCount];

                for (var s = 0; s < newStateCount; s++)
                {
                    if (core.ResidencyOffset < 0)
                    {
                        var name = ToManagedString(IOReportStateGetNameForIndex(item, s));
                        if (name is not ("IDLE" or "DOWN" or "OFF"))
                            core.ResidencyOffset = s;
                    }

                    core.PrevResidencies[s] = IOReportStateGetResidency(item, s);
                }

                if (isECore)
                    eCores.Add(core);
                else
                    pCores.Add(core);
                cores.Add(core);
            }
            else
            {
                // 2回目以降: 周波数を更新する
                var stateCount = IOReportStateGetCount(item);
                for (var s = 0; s < stateCount && s < core.CurrResidencies.Length; s++)
                    core.CurrResidencies[s] = IOReportStateGetResidency(item, s);

                core.Frequency = CalculateFrequencies(core.CurrResidencies, core.PrevResidencies, core.FreqTable, core.ResidencyOffset);

                // バッファをスワップ: 今回値が次回の前回値になる / Swap buffers: current becomes previous for the next call
                (core.PrevResidencies, core.CurrResidencies) = (core.CurrResidencies, core.PrevResidencies);
            }
        }

        UpdateAt = DateTime.Now;
        return true;
    }

    private CpuCoreFrequency? FindCore(string channelName, bool isECore)
    {
        var list = isECore ? eCores : pCores;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].ChannelName == channelName)
                return list[i];
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
