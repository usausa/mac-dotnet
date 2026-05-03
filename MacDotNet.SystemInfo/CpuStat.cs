namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;
using System.Text;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class CpuCoreStat
{
    public int Number { get; }

    public CpuCoreType CoreType { get; }

    internal int LogicalCpuId { get; }

    public uint User { get; internal set; }

    public uint System { get; internal set; }

    public uint Idle { get; internal set; }

    public uint Nice { get; internal set; }

    internal CpuCoreStat(int number, CpuCoreType coreType, int logicalCpuId)
    {
        Number = number;
        CoreType = coreType;
        LogicalCpuId = logicalCpuId;
    }
}

public sealed class CpuStat
{
    private static readonly Lazy<IReadOnlyDictionary<int, CpuCoreType>> CoreTypes = new(ReadCoreTypes);

    private readonly List<CpuCoreStat> cpuCores = [];

    private readonly List<CpuCoreStat> efficiencyCores = [];

    private readonly List<CpuCoreStat> performanceCores = [];

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<CpuCoreStat> CpuCores => cpuCores;

    public IReadOnlyList<CpuCoreStat> EfficiencyCores => efficiencyCores;

    public IReadOnlyList<CpuCoreStat> PerformanceCores => performanceCores;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal CpuStat()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        using var host = new MachPortRef(mach_host_self());
        var result = host_processor_info(host, PROCESSOR_CPU_LOAD_INFO, out var processorCount, out var info, out var infoCount);
        if (result != KERN_SUCCESS)
        {
            return false;
        }

        try
        {
            var ptr = (uint*)info;

            if (cpuCores.Count < processorCount)
            {
                while (cpuCores.Count < processorCount)
                {
                    var logicalCpuId = cpuCores.Count;
                    var coreType = CoreTypes.Value.GetValueOrDefault(logicalCpuId, CpuCoreType.Unknown);
                    var number = coreType switch
                    {
                        CpuCoreType.Efficiency => efficiencyCores.Count,
                        CpuCoreType.Performance => performanceCores.Count,
                        _ => logicalCpuId
                    };
                    var core = new CpuCoreStat(number, coreType, logicalCpuId);

                    cpuCores.Add(core);

                    if (coreType == CpuCoreType.Efficiency)
                    {
                        efficiencyCores.Add(core);
                    }
                    else if (coreType == CpuCoreType.Performance)
                    {
                        performanceCores.Add(core);
                    }
                }

                cpuCores.Sort(static (x, y) =>
                {
                    var cmp = (int)x.CoreType - (int)y.CoreType;
                    return cmp != 0 ? cmp : x.Number - y.Number;
                });
            }

            foreach (var core in cpuCores)
            {
                var offset = core.LogicalCpuId * CPU_STATE_MAX;
                var user = ptr[offset + CPU_STATE_USER];
                var system = ptr[offset + CPU_STATE_SYSTEM];
                var idle = ptr[offset + CPU_STATE_IDLE];
                var nice = ptr[offset + CPU_STATE_NICE];

                core.User = user;
                core.System = system;
                core.Idle = idle;
                core.Nice = nice;
            }

            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            _ = vm_deallocate(task_self_trap(), info, sizeof(int) * infoCount);
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static Dictionary<int, CpuCoreType> ReadCoreTypes()
    {
        var coreTypes = new Dictionary<int, CpuCoreType>();
        var matching = IOServiceMatching("AppleARMPE");
        if (matching == IntPtr.Zero)
        {
            return coreTypes;
        }

        var iterator = IntPtr.Zero;
        if (IOServiceGetMatchingServices(0, matching, ref iterator) != KERN_SUCCESS || iterator == IntPtr.Zero)
        {
            return coreTypes;
        }

        using var services = new IORef(iterator);
        uint service;
        while ((service = IOIteratorNext(services)) != 0)
        {
            using var serviceObject = new IOObj(service);
            if (IORegistryEntryGetChildIterator(serviceObject, "IOService", out var childIterator) != KERN_SUCCESS || childIterator == IntPtr.Zero)
            {
                continue;
            }

            using var children = new IORef(childIterator);
            uint child;
            while ((child = IOIteratorNext(children)) != 0)
            {
                using var childObject = new IOObj(child);
                var name = GetEntryName(childObject);
                if (string.IsNullOrEmpty(name) || !name.StartsWith("cpu", StringComparison.Ordinal) || (name.Length <= 3) || !char.IsDigit(name[3]))
                {
                    continue;
                }

                if (IORegistryEntryCreateCFProperties(childObject, out var properties, IntPtr.Zero, 0) != KERN_SUCCESS || properties == IntPtr.Zero)
                {
                    continue;
                }

                using var values = new CFRef(properties);
                var logicalCpuId = GetLogicalCpuId(values);
                if (logicalCpuId < 0)
                {
                    continue;
                }

                coreTypes[logicalCpuId] = GetCoreType(values);
            }
        }

        return coreTypes;
    }

    private static unsafe string? GetEntryName(IOObj entry)
    {
        var buffer = stackalloc byte[128];
        return IORegistryEntryGetName(entry, buffer) == KERN_SUCCESS ? Marshal.PtrToStringUTF8((IntPtr)buffer) : null;
    }

    private static int GetLogicalCpuId(CFRef properties)
    {
        using var key = CFRef.CreateString("logical-cpu-id");
        if (!key.IsValid)
        {
            return -1;
        }

        var value = CFDictionaryGetValue(properties, key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return -1;
        }

        return CFNumberGetValue(value, kCFNumberSInt32Type, out var logicalCpuId) ? logicalCpuId : -1;
    }

    private static CpuCoreType GetCoreType(CFRef properties)
    {
        using var key = CFRef.CreateString("cluster-type");
        if (!key.IsValid)
        {
            return CpuCoreType.Unknown;
        }

        var value = CFDictionaryGetValue(properties, key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFDataGetTypeID()))
        {
            return CpuCoreType.Unknown;
        }

        var length = CFDataGetLength(value).ToInt32();
        if (length <= 0)
        {
            return CpuCoreType.Unknown;
        }

        var bytes = new byte[length];
        Marshal.Copy(CFDataGetBytePtr(value), bytes, 0, length);
        var clusterType = Encoding.UTF8.GetString(bytes).TrimEnd('\0');

        return clusterType switch
        {
            "E" => CpuCoreType.Efficiency,
            "P" => CpuCoreType.Performance,
            _ => CpuCoreType.Unknown
        };
    }
}
