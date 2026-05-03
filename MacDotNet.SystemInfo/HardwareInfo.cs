namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed record GpuInfo
{
    // Basic

    public string Model { get; init; } = default!;

    public string Name { get; init; } = default!;

    public string MetalPluginName { get; init; } = default!;

    public int CoreCount { get; init; }

    public uint VendorId { get; init; }

    // Configuration

    public int GpuGeneration { get; init; }

    public int NumCores { get; init; }

    public int NumGPs { get; init; }

    public int NumFragments { get; init; }

    public int NumMGpus { get; init; }

    public int UscGeneration { get; init; }
}

public sealed record CorePerformanceLevel
{
    public string Name { get; init; } = default!;

    public int PhysicalCpu { get; init; }

    public int LogicalCpu { get; init; }

    public int CpusPerL2 { get; init; }

    public int L2CacheSize { get; init; }

    public int CpuFrequencyMax { get; init; }
}

// ReSharper disable StringLiteralTypo
public sealed class HardwareInfo
{
    public string Model { get; }

    public string Machine { get; }

    public string TargetType { get; }

    public string CpuBrandString { get; }

    public string SerialNumber { get; }

    public int LogicalCpu { get; }

    public int LogicalCpuMax { get; }

    public int PhysicalCpu { get; }

    public int PhysicalCpuMax { get; }

    public int ActiveCpu { get; }

    public int CpuCoreCount { get; }

    public int CpuThreadCount { get; }

    public ulong TimebaseFrequency { get; }

    public ulong MemorySize { get; }

    public ulong PageSize { get; }

    public ulong CacheLineSize { get; }

    public ulong L1ICacheSize { get; }

    public ulong L1DCacheSize { get; }

    public ulong L2CacheSize { get; }

    public ulong L3CacheSize { get; }

    public int Packages { get; }

    public int PerformanceCoreCount { get; }

    public int EfficiencyCoreCount { get; }

    public CorePerformanceLevel PerformanceCoreLevel { get; }

    public CorePerformanceLevel EfficiencyCoreLevel { get; }

    public IReadOnlyList<GpuInfo> Gpus { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal HardwareInfo()
    {
        Model = GetSystemControlString("hw.model") ?? string.Empty;
        Machine = GetSystemControlString("hw.machine") ?? string.Empty;
        TargetType = GetSystemControlString("hw.targettype") ?? string.Empty;
        CpuBrandString = GetSystemControlString("machdep.cpu.brand_string") ?? string.Empty;
        SerialNumber = GetSerialNumber() ?? string.Empty;
        LogicalCpu = GetSystemControlInt32("hw.logicalcpu");
        LogicalCpuMax = GetSystemControlInt32("hw.logicalcpu_max");
        PhysicalCpu = GetSystemControlInt32("hw.physicalcpu");
        PhysicalCpuMax = GetSystemControlInt32("hw.physicalcpu_max");
        ActiveCpu = GetSystemControlInt32("hw.activecpu");
        CpuCoreCount = GetSystemControlInt32("machdep.cpu.core_count");
        CpuThreadCount = GetSystemControlInt32("machdep.cpu.thread_count");
        TimebaseFrequency = GetSystemControlUInt64("hw.tbfrequency");
        MemorySize = GetSystemControlUInt64("hw.memsize");
        PageSize = GetSystemControlUInt64("hw.pagesize");
        CacheLineSize = GetSystemControlUInt64("hw.cachelinesize");
        L1ICacheSize = GetSystemControlUInt64("hw.l1icachesize");
        L1DCacheSize = GetSystemControlUInt64("hw.l1dcachesize");
        L2CacheSize = GetSystemControlUInt64("hw.l2cachesize");
        L3CacheSize = GetSystemControlUInt64("hw.l3cachesize");
        Packages = GetSystemControlInt32("hw.packages");

        var levels = GetSystemControlInt32("hw.nperflevels");
        PerformanceCoreCount = levels > 0 ? GetSystemControlInt32("hw.perflevel0.logicalcpu") : 0;
        EfficiencyCoreCount = levels > 1 ? GetSystemControlInt32("hw.perflevel1.logicalcpu") : 0;
        PerformanceCoreLevel = levels > 0 ? ReadCorePerformanceLevel(0) : new CorePerformanceLevel();
        EfficiencyCoreLevel = levels > 1 ? ReadCorePerformanceLevel(1) : new CorePerformanceLevel();

        Gpus = ReadGpus();
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static string? GetSerialNumber()
    {
        var matching = IOServiceMatching("IOPlatformExpertDevice");
        if (matching == IntPtr.Zero)
        {
            return null;
        }

        using var service = new IOObj(IOServiceGetMatchingService(0, matching));
        if (!service.IsValid)
        {
            return null;
        }

        using var key = CFRef.CreateString("IOPlatformSerialNumber");
        using var value = new CFRef(IORegistryEntryCreateCFProperty(service, key, IntPtr.Zero, 0));
        return value.IsValid ? value.GetString() : null;
    }

    private static CorePerformanceLevel ReadCorePerformanceLevel(int index)
    {
        var name = GetSystemControlString($"hw.perflevel{index}.name") ?? $"Level {index}";
        return new CorePerformanceLevel
        {
            Name = name,
            PhysicalCpu = GetSystemControlInt32($"hw.perflevel{index}.physicalcpu"),
            LogicalCpu = GetSystemControlInt32($"hw.perflevel{index}.logicalcpu"),
            CpusPerL2 = GetSystemControlInt32($"hw.perflevel{index}.cpusperl2"),
            L2CacheSize = GetSystemControlInt32($"hw.perflevel{index}.l2cachesize"),
            CpuFrequencyMax = GetSystemControlInt32($"hw.perflevel{index}.cpufreq_max")
        };
    }

    private static List<GpuInfo> ReadGpus()
    {
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), out var itHandle);
        if ((kr != KERN_SUCCESS) || (itHandle == 0))
        {
            return [];
        }

        using var it = new IORef(itHandle);
        var results = new List<GpuInfo>();
        uint raw;
        while ((raw = IOIteratorNext(it)) != 0)
        {
            using var entry = new IOObj(raw);

            var info = new GpuInfo
            {
                Model = entry.GetString("model") ?? string.Empty,
                Name = entry.GetString("IOClass") ?? string.Empty,
                MetalPluginName = entry.GetString("MetalPluginName") ?? string.Empty,
                CoreCount = (int)entry.GetUInt64("gpu-core-count"),
                VendorId = entry.GetDataUInt32("vendor-id")
            };

            using var dict = entry.GetDictionary("GPUConfigurationVariable");
            if (dict.IsValid)
            {
                info = info with
                {
                    GpuGeneration = (int)dict.GetInt64("gpu_gen"),
                    NumCores = (int)dict.GetInt64("num_cores"),
                    NumGPs = (int)dict.GetInt64("num_gps"),
                    NumFragments = (int)dict.GetInt64("num_frags"),
                    NumMGpus = (int)dict.GetInt64("num_mgpus"),
                    UscGeneration = (int)dict.GetInt64("usc_gen")
                };
            }

            results.Add(info);
        }

        return results;
    }
    // ReSharper restore StringLiteralTypo
}
