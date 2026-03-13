namespace MacDotNet.SystemInfo;

using System.Buffers.Binary;
using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class CpuCoreFrequency
{
    public int Number { get; }

    public CpuCoreType CoreType { get; }

    public double Frequency { get; internal set; }

#pragma warning disable SA1401
    internal string ChannelName = string.Empty;

    internal int[] FrequencyTable = [];

    internal long[] PreviousResidencies = [];

    internal long[] CurrentResidencies = [];

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
    private readonly int[] eCoreFrequencyTable;
    private readonly int[] pCoreFrequencyTable;

    private readonly List<CpuCoreFrequency> cores = [];

    private readonly List<CpuCoreFrequency> efficiencyCores = [];

    private readonly List<CpuCoreFrequency> performanceCores = [];

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<CpuCoreFrequency> Cores => cores;

    public IReadOnlyList<CpuCoreFrequency> EfficiencyCores => efficiencyCores;

    public IReadOnlyList<CpuCoreFrequency> PerformanceCores => performanceCores;

    public int MaxEfficiencyCoreFrequency => eCoreFrequencyTable.Length > 0 ? eCoreFrequencyTable[^1] : 0;

    public int MaxPerformanceCoreFrequency => pCoreFrequencyTable.Length > 0 ? pCoreFrequencyTable[^1] : 0;

        //--------------------------------------------------------------------------------
        // Constructor
        //--------------------------------------------------------------------------------

    internal unsafe CpuFrequency()
    {
        var cpuName = GetSystemControlString("machdep.cpu.brand_string") ?? string.Empty;
        var multiplier = cpuName.Contains("M4", StringComparison.OrdinalIgnoreCase) || cpuName.Contains("M5", StringComparison.OrdinalIgnoreCase)
            ? 1000u
            : 1_000_000u;

        eCoreFrequencyTable = [];
        pCoreFrequencyTable = [];

        var matching = IOServiceMatching("AppleARMIODevice");
        if (matching != IntPtr.Zero && IOServiceGetMatchingServices(0, matching, out var iterator) == 0)
        {
            var nameBuf = stackalloc byte[128];

            using var it = new IOObj(iterator);
            uint child;
            while ((child = IOIteratorNext(it)) != 0)
            {
                using var entry = new IOObj(child);

                if (IORegistryEntryGetName(entry, nameBuf) != 0 ||
                    Marshal.PtrToStringUTF8((IntPtr)nameBuf) != "pmgr" ||
                    IORegistryEntryCreateCFProperties(entry, out var propsRef, IntPtr.Zero, 0) != 0)
                {
                    continue;
                }

                using var props = new CFRef(propsRef);

                using var eKey = CFRef.CreateString("voltage-states1-sram");
                var eData = CFDictionaryGetValue(props, eKey);
                if (eData != IntPtr.Zero)
                {
                    eCoreFrequencyTable = ConvertToFrequencyTable(eData, multiplier);
                }

                using var pKey = CFRef.CreateString("voltage-states5-sram");
                var pData = CFDictionaryGetValue(props, pKey);
                if (pData != IntPtr.Zero)
                {
                    pCoreFrequencyTable = ConvertToFrequencyTable(pData, multiplier);
                }
            }
        }

        Update();
    }

    private static unsafe int[] ConvertToFrequencyTable(IntPtr cfData, uint multiplier)
    {
        var length = (int)CFDataGetLength(cfData);
        var ptr = CFDataGetBytePtr(cfData);
        var bytes = new ReadOnlySpan<byte>((void*)ptr, length);

        var result = new int[length / 8];
        for (var i = 0; i < result.Length; i++)
        {
            var v = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * 8, 4));
            result[i] = (int)(v / multiplier);
        }

        return result;
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

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

        using var sample = new CFRef(IOReportCreateSamples(subscription, channels, IntPtr.Zero));
        if (!sample.IsValid)
        {
            return false;
        }

        using var key = CFRef.CreateString("IOReportChannels");
        var items = CFDictionaryGetValue(sample, key);
        if (items == IntPtr.Zero)
        {
            return false;
        }

        for (var i = 0; i < cores.Count; i++)
        {
            cores[i].Frequency = 0;
        }

        var count = CFArrayGetCount(items);
        for (var idx = 0L; idx < count; idx++)
        {
            var item = CFArrayGetValueAtIndex(items, idx);
            var channelName = ToManagedString(IOReportChannelGetChannelName(item));
            if (channelName is null)
            {
                continue;
            }

            var isECore = channelName.StartsWith("ECPU", StringComparison.Ordinal);
            var targetCores = isECore ? efficiencyCores : performanceCores;
            var core = FindCore(targetCores, channelName);
            if (core is null)
            {
                // 初回: チャンネルに対応するコアを新規作成し PreviousResidencies にベースラインを記録する
                var coreType = isECore ? CpuCoreType.Efficiency : CpuCoreType.Performance;
                core = new CpuCoreFrequency(targetCores.Count, coreType)
                {
                    ChannelName = channelName,
                    FrequencyTable = isECore ? eCoreFrequencyTable : pCoreFrequencyTable
                };

                var newStateCount = IOReportStateGetCount(item);
                core.PreviousResidencies = new long[newStateCount];
                core.CurrentResidencies = new long[newStateCount];

                for (var s = 0; s < newStateCount; s++)
                {
                    if (core.ResidencyOffset < 0)
                    {
                        var name = ToManagedString(IOReportStateGetNameForIndex(item, s));
                        if (name is not ("IDLE" or "DOWN" or "OFF"))
                        {
                            core.ResidencyOffset = s;
                        }
                    }

                    core.PreviousResidencies[s] = IOReportStateGetResidency(item, s);
                }

                targetCores.Add(core);
                cores.Add(core);
            }
            else
            {
                // 2回目以降: 周波数を更新する
                var stateCount = IOReportStateGetCount(item);
                for (var s = 0; s < stateCount && s < core.CurrentResidencies.Length; s++)
                {
                    core.CurrentResidencies[s] = IOReportStateGetResidency(item, s);
                }

                core.Frequency = CalculateFrequencies(core.CurrentResidencies, core.PreviousResidencies, core.FrequencyTable, core.ResidencyOffset);

                // バッファをスワップ: 今回値が次回の前回値になる / Swap buffers: current becomes previous for the next call
                (core.PreviousResidencies, core.CurrentResidencies) = (core.CurrentResidencies, core.PreviousResidencies);
            }
        }

        UpdateAt = DateTime.Now;
        return true;
    }

    private static IntPtr GetChannels()
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

    private static CpuCoreFrequency? FindCore(List<CpuCoreFrequency> list, string channelName)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].ChannelName == channelName)
            {
                return list[i];
            }
        }

        return null;
    }

    /// 前回・今回のレジデンシー差分から実効周波数 (MHz) を算出する。
    /// 全ステートの差分合計を分母にすることでアイドル時間を反映した実効周波数を得る。
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
}
