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
    private static readonly Lazy<(int[] ECore, int[] PCore)> FrequencyTables = new(ReadFrequencyTables);

    private readonly List<CpuCoreFrequency> cores = [];

    private readonly List<CpuCoreFrequency> efficiencyCores = [];

    private readonly List<CpuCoreFrequency> performanceCores = [];

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<CpuCoreFrequency> Cores => cores;

    public IReadOnlyList<CpuCoreFrequency> EfficiencyCores => efficiencyCores;

    public IReadOnlyList<CpuCoreFrequency> PerformanceCores => performanceCores;

#pragma warning disable CA1822
    public int MaxEfficiencyCoreFrequency => FrequencyTables.Value.ECore.Length > 0 ? FrequencyTables.Value.ECore[^1] : 0;

    public int MaxPerformanceCoreFrequency => FrequencyTables.Value.PCore.Length > 0 ? FrequencyTables.Value.PCore[^1] : 0;
#pragma warning restore CA1822

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal CpuFrequency()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // ReSharper disable StringLiteralTypo
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

        var coreAdded = false;
        var count = CFArrayGetCount(items);
        for (var i = 0L; i < count; i++)
        {
            var item = CFArrayGetValueAtIndex(items, i);
            var channelName = ToManagedString(IOReportChannelGetChannelName(item));
            if (channelName is null)
            {
                continue;
            }

            var isEfficiencyCore = channelName.StartsWith("ECPU", StringComparison.Ordinal);
            var targetCores = isEfficiencyCore ? efficiencyCores : performanceCores;

            var core = FindCore(targetCores, channelName);
            if (core is null)
            {
                var coreType = isEfficiencyCore ? CpuCoreType.Efficiency : CpuCoreType.Performance;
                core = new CpuCoreFrequency(targetCores.Count, coreType)
                {
                    ChannelName = channelName,
                    FrequencyTable = isEfficiencyCore ? FrequencyTables.Value.ECore : FrequencyTables.Value.PCore
                };

                var newStateCount = IOReportStateGetCount(item);
                core.PreviousResidencies = new long[newStateCount];
                core.CurrentResidencies = new long[newStateCount];

                // Calc residency offset
                for (var j = 0; j < newStateCount; j++)
                {
                    if (core.ResidencyOffset < 0)
                    {
                        var name = ToManagedString(IOReportStateGetNameForIndex(item, j));
                        if (name is not ("IDLE" or "DOWN" or "OFF"))
                        {
                            core.ResidencyOffset = j;
                        }
                    }

                    core.PreviousResidencies[j] = IOReportStateGetResidency(item, j);
                }

                targetCores.Add(core);
                cores.Add(core);
                coreAdded = true;
            }
            else
            {
                // Update frequency
                var stateCount = IOReportStateGetCount(item);
                for (var j = 0; j < stateCount && j < core.CurrentResidencies.Length; j++)
                {
                    core.CurrentResidencies[j] = IOReportStateGetResidency(item, j);
                }

                var freq = CalculateFrequencies(core.CurrentResidencies, core.PreviousResidencies, core.FrequencyTable, core.ResidencyOffset);
                var minFreq = core.FrequencyTable.Length > 0 ? core.FrequencyTable[0] : 0;
                core.Frequency = Math.Max(freq, minFreq);

                // Swap current to previous for the next round
                (core.PreviousResidencies, core.CurrentResidencies) = (core.CurrentResidencies, core.PreviousResidencies);
            }
        }

        if (coreAdded)
        {
            cores.Sort(static (x, y) =>
            {
                var cmp = x.CoreType.CompareTo(y.CoreType);
                return cmp != 0 ? cmp : x.Number.CompareTo(y.Number);
            });
        }

        UpdateAt = DateTime.Now;

        return true;
    }
    // ReSharper restore StringLiteralTypo

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // ReSharper disable StringLiteralTypo
    private static unsafe (int[] ECore, int[] PCore) ReadFrequencyTables()
    {
        var cpuName = GetSystemControlString("machdep.cpu.brand_string") ?? string.Empty;
        var multiplier = cpuName.Contains("M4", StringComparison.OrdinalIgnoreCase) || cpuName.Contains("M5", StringComparison.OrdinalIgnoreCase)
            ? 1000u
            : 1_000_000u;

        int[] eCore = [];
        int[] pCore = [];

        var matching = IOServiceMatching("AppleARMIODevice");
        if ((matching != IntPtr.Zero) && (IOServiceGetMatchingServices(0, matching, out var iterator) == 0))
        {
            var nameBuf = stackalloc byte[128];

            using var it = new IOObj(iterator);
            uint child;
            while ((child = IOIteratorNext(it)) != 0)
            {
                using var entry = new IOObj(child);

                if ((IORegistryEntryGetName(entry, nameBuf) != 0) ||
                    (Marshal.PtrToStringUTF8((IntPtr)nameBuf) != "pmgr") ||
                    (IORegistryEntryCreateCFProperties(entry, out var propsRef, IntPtr.Zero, 0) != 0))
                {
                    continue;
                }

                using var props = new CFRef(propsRef);

                using var eKey = CFRef.CreateString("voltage-states1-sram");
                var eData = CFDictionaryGetValue(props, eKey);
                if (eData != IntPtr.Zero)
                {
                    eCore = ConvertToFrequencyTable(eData, multiplier);
                }

                using var pKey = CFRef.CreateString("voltage-states5-sram");
                var pData = CFDictionaryGetValue(props, pKey);
                if (pData != IntPtr.Zero)
                {
                    pCore = ConvertToFrequencyTable(pData, multiplier);
                }
            }
        }

        return (eCore, pCore);
    }
    // ReSharper restore StringLiteralTypo

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

    private static IntPtr GetChannels()
    {
        using var group = CFRef.CreateString("CPU Stats");
        using var subGroup = CFRef.CreateString("CPU Core Performance States");
        using var channel = new CFRef(IOReportCopyChannelsInGroup(group, subGroup, 0, 0, 0));
        if (!channel.IsValid)
        {
            return IntPtr.Zero;
        }

        // IOReportCreateSubscription requires a mutable dictionary
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

    private static double CalculateFrequencies(long[] currentValues, long[] previousValues, int[] table, int offset)
    {
        if (offset < 0)
        {
            return 0;
        }

        var activeDelta = 0L;
        for (var i = offset; i < currentValues.Length; i++)
        {
            activeDelta += currentValues[i] - previousValues[i];
        }

        if (activeDelta == 0)
        {
            return 0;
        }

        var avgFreq = 0d;
        for (var i = 0; i < table.Length; i++)
        {
            var key = i + offset;
            if (key >= currentValues.Length)
            {
                continue;
            }

            var delta = currentValues[key] - previousValues[key];
            avgFreq += (double)delta / activeDelta * table[i];
        }

        return avgFreq;
    }
}
