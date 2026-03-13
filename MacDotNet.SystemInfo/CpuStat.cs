namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;
using System.Text;

using static MacDotNet.SystemInfo.NativeMethods;

public enum CpuCoreType
{
    Unknown = -1,
    Total = Int32.MaxValue,
    Efficiency = 0,
    Performance = 1
}

public sealed class CpuCoreStat
{
    public string Name { get; }

    public CpuCoreType CoreType { get; }

    public uint User { get; internal set; }

    public uint System { get; internal set; }

    public uint Idle { get; internal set; }

    public uint Nice { get; internal set; }

    internal CpuCoreStat(CpuCoreType coreType, string name)
    {
        CoreType = coreType;
        Name = name;
    }
}

// TODO Frequency

public sealed class CpuStat
{
    // TODO
    private readonly List<CpuCoreStat> cpuCores = [];

    private readonly List<CpuCoreStat> efficiencyCores = [];

    private readonly List<CpuCoreStat> performanceCores = [];

    private static readonly Lazy<IReadOnlyDictionary<int, CpuCoreType>> CoreTypes = new(valueFactory: ReadCoreTypes);

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
        using var host = new MachPortRef(port: mach_host_self());
        var result = host_processor_info(host: host, flavor: PROCESSOR_CPU_LOAD_INFO, processorCount: out var processorCount, processorInfo: out var info, processorInfoCnt: out var infoCount);
        if (result != KERN_SUCCESS)
        {
            return false;
        }

        try
        {
            var ptr = (uint*)info;

            while (cpuCores.Count < processorCount)
            {
                var logicalCpuId = cpuCores.Count;
                var coreType = CoreTypes.Value.GetValueOrDefault(key: logicalCpuId, defaultValue: CpuCoreType.Unknown);
                var core = new CpuCoreStat(coreType: coreType, name: $"{logicalCpuId}");

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

            for (var i = 0; i < processorCount; i++)
            {
                var offset = i * CPU_STATE_MAX;
                var user = ptr[offset + CPU_STATE_USER];
                var system = ptr[offset + CPU_STATE_SYSTEM];
                var idle = ptr[offset + CPU_STATE_IDLE];
                var nice = ptr[offset + CPU_STATE_NICE];

                cpuCores[i].User = user;
                cpuCores[i].System = system;
                cpuCores[i].Idle = idle;
                cpuCores[i].Nice = nice;
            }

            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            _ = vm_deallocate(targetTask: task_self_trap(), address: info, size: sizeof(int) * infoCount);
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static Dictionary<int, CpuCoreType> ReadCoreTypes()
    {
        var coreTypes = new Dictionary<int, CpuCoreType>();
        var matching = IOServiceMatching(name: "AppleARMPE");
        if (matching == IntPtr.Zero)
        {
            return coreTypes;
        }

        var iterator = IntPtr.Zero;
        if (IOServiceGetMatchingServices(mainPort: 0, matching: matching, existing: ref iterator) != KERN_SUCCESS || iterator == IntPtr.Zero)
        {
            return coreTypes;
        }

        using var services = new IORef(pointer: iterator);
        uint service;
        while ((service = IOIteratorNext(iterator: services)) != 0)
        {
            using var serviceObject = new IOObj(handle: service);
            if (IORegistryEntryGetChildIterator(entry: serviceObject, plane: "IOService", iterator: out var childIterator) != KERN_SUCCESS || childIterator == IntPtr.Zero)
            {
                continue;
            }

            using var children = new IORef(pointer: childIterator);
            uint child;
            while ((child = IOIteratorNext(iterator: children)) != 0)
            {
                using var childObject = new IOObj(handle: child);
                var name = GetEntryName(entry: childObject);
                if (string.IsNullOrEmpty(value: name) || !name.StartsWith(value: "cpu", comparisonType: StringComparison.Ordinal) || (name.Length <= 3) || !char.IsDigit(c: name[index: 3]))
                {
                    continue;
                }

                if (IORegistryEntryCreateCFProperties(entry: childObject, properties: out var properties, allocator: IntPtr.Zero, options: 0) != KERN_SUCCESS || properties == IntPtr.Zero)
                {
                    continue;
                }

                using var values = new CFRef(pointer: properties);
                var logicalCpuId = GetLogicalCpuId(properties: values);
                if (logicalCpuId < 0)
                {
                    continue;
                }

                coreTypes[key: logicalCpuId] = GetCoreType(properties: values);
            }
        }

        return coreTypes;
    }

    private static unsafe string? GetEntryName(IOObj entry)
    {
        var buffer = stackalloc byte[128];
        return IORegistryEntryGetName(entry: entry, name: buffer) == KERN_SUCCESS ? Marshal.PtrToStringUTF8(ptr: (IntPtr)buffer) : null;
    }

    private static int GetLogicalCpuId(CFRef properties)
    {
        using var key = CFRef.CreateString(s: "logical-cpu-id");
        if (!key.IsValid)
        {
            return -1;
        }

        var value = CFDictionaryGetValue(theDict: properties, key: key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(cf: value) != CFNumberGetTypeID()))
        {
            return -1;
        }

        return CFNumberGetValue(number: value, theType: kCFNumberSInt32Type, valuePtr: out var logicalCpuId) ? logicalCpuId : -1;
    }

    private static CpuCoreType GetCoreType(CFRef properties)
    {
        using var key = CFRef.CreateString(s: "cluster-type");
        if (!key.IsValid)
        {
            return CpuCoreType.Unknown;
        }

        var value = CFDictionaryGetValue(theDict: properties, key: key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(cf: value) != CFDataGetTypeID()))
        {
            return CpuCoreType.Unknown;
        }

        var length = CFDataGetLength(theData: value).ToInt32();
        if (length <= 0)
        {
            return CpuCoreType.Unknown;
        }

        var bytes = new byte[length];
        Marshal.Copy(source: CFDataGetBytePtr(theData: value), destination: bytes, startIndex: 0, length: length);
        var clusterType = Encoding.UTF8.GetString(bytes: bytes).TrimEnd(trimChar: '\0');

        return clusterType switch
        {
            "E" => CpuCoreType.Efficiency,
            "P" => CpuCoreType.Performance,
            _ => CpuCoreType.Unknown
        };
    }
}
