namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// GPU の静的なハードウェア情報。GpuDevice.Name と GpuInfo.Name で動的情報とマッチングできる。
/// <para>Static GPU hardware info. Match with runtime GpuDevice via GpuDevice.Name == GpuInfo.Name.</para>
/// </summary>
public sealed record GpuInfo
{
    /// <summary>GPU モデル名。例: "Apple M2 Pro"<br/>GPU model name. Example: "Apple M2 Pro"</summary>
    public required string Model { get; init; }

    /// <summary>IOKit のクラス名。GpuDevice.Name と一致するマッチングキー。例: "AGXAcceleratorG14X"<br/>IOKit class name. Matching key equal to GpuDevice.Name. Example: "AGXAcceleratorG14X"</summary>
    public required string Name { get; init; }

    /// <summary>Metal プラグイン名。例: "AGXMetalG14X"。取得できない場合は空文字列<br/>Metal plugin name. Example: "AGXMetalG14X". Empty string if unavailable.</summary>
    public string MetalPluginName { get; init; } = string.Empty;

    /// <summary>GPU コア数<br/>Number of GPU cores</summary>
    public required int CoreCount { get; init; }

    /// <summary>GPU ベンダー ID。例: Apple Silicon の場合は 0x106B<br/>GPU vendor ID. Example: 0x106B for Apple Silicon</summary>
    public required uint VendorId { get; init; }

    // GPUConfigurationVariable (取得できない場合は 0)

    /// <summary>GPU アーキテクチャ世代番号 (GPUConfigurationVariable)。取得できない場合は 0<br/>GPU architecture generation number (GPUConfigurationVariable). 0 if unavailable.</summary>
    public int GpuGeneration { get; init; }

    /// <summary>GPU コア (Execution Unit) の数 (GPUConfigurationVariable)。取得できない場合は 0<br/>Number of GPU cores / Execution Units (GPUConfigurationVariable). 0 if unavailable.</summary>
    public int NumCores { get; init; }

    /// <summary>ジオメトリプロセッサ (GP) の数 (GPUConfigurationVariable)。取得できない場合は 0<br/>Number of geometry processors (GPUConfigurationVariable). 0 if unavailable.</summary>
    public int NumGPs { get; init; }

    /// <summary>フラグメントシェーダーユニットの数 (GPUConfigurationVariable)。取得できない場合は 0<br/>Number of fragment shader units (GPUConfigurationVariable). 0 if unavailable.</summary>
    public int NumFragments { get; init; }

    /// <summary>マルチ GPU の数 (GPUConfigurationVariable)。取得できない場合は 0<br/>Number of multi-GPU units (GPUConfigurationVariable). 0 if unavailable.</summary>
    public int NumMGpus { get; init; }

    /// <summary>USC (Unified Shader Core) のアーキテクチャ世代番号 (GPUConfigurationVariable)。取得できない場合は 0<br/>USC architecture generation number (GPUConfigurationVariable). 0 if unavailable.</summary>
    public int UscGeneration { get; init; }
}

/// <summary>
/// CPU コアタイプ (P-core / E-core) ごとの静的パフォーマンスレベル情報。CoreType で CpuCoreStat / CpuCoreFrequency とマッチングできる。
/// <para>Static performance level info per CPU core type (P-core / E-core). Match with CpuCoreStat / CpuCoreFrequency via CoreType.</para>
/// </summary>
public sealed record CorePerformanceLevel
{
    /// <summary>コアの種別。CpuCoreStat.CoreType / CpuCoreFrequency.CoreType とのマッチングキー。サポートされない場合は Unknown<br/>Core type. Matching key for CpuCoreStat.CoreType / CpuCoreFrequency.CoreType. Unknown if unsupported.</summary>
    public CpuCoreType CoreType { get; init; } = CpuCoreType.Unknown;

    /// <summary>パフォーマンスレベルのインデックス。0 が最高性能 (P-core)、1 以降が省電力 (E-core)。サポートされない場合は 0<br/>Performance level index. 0 = highest performance (P-core), 1+ = efficiency (E-core). 0 if unsupported.</summary>
    public int Index { get; init; }

    /// <summary>レベル名 (hw.perflevelN.name)。例: "Performance"、"Efficiency"。サポートされない場合は空文字列<br/>Level name (hw.perflevelN.name). Example: "Performance", "Efficiency". Empty string if unsupported.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>このレベルに属する物理コア数 (hw.perflevelN.physicalcpu)。サポートされない場合は 0<br/>Number of physical CPUs at this level (hw.perflevelN.physicalcpu). 0 if unsupported.</summary>
    public int PhysicalCpu { get; init; }

    /// <summary>このレベルに属する論理コア数 (hw.perflevelN.logicalcpu)。サポートされない場合は 0<br/>Number of logical CPUs at this level (hw.perflevelN.logicalcpu). 0 if unsupported.</summary>
    public int LogicalCpu { get; init; }

    /// <summary>L2 キャッシュを共有する CPU の数 (hw.perflevelN.cpusperl2)。サポートされない場合は 0<br/>Number of CPUs sharing an L2 cache (hw.perflevelN.cpusperl2). 0 if unsupported.</summary>
    public int CpusPerL2 { get; init; }

    /// <summary>L2 キャッシュサイズ (バイト) (hw.perflevelN.l2cachesize)。サポートされない場合は 0<br/>L2 cache size in bytes (hw.perflevelN.l2cachesize). 0 if unsupported.</summary>
    public int L2CacheSize { get; init; }

    /// <summary>このレベルの CPU 最大周波数 (Hz) (hw.perflevelN.cpufreq_max)。取得できない場合は 0<br/>Maximum CPU frequency in Hz (hw.perflevelN.cpufreq_max). 0 if unavailable.</summary>
    public int CpuFrequencyMax { get; init; }
}

/// <summary>
/// CPU・メモリ・キャッシュなどハードウェア構成の静的情報。sysctl で取得する。
/// <para>Static hardware configuration info for CPU, memory, and cache. Retrieved via sysctl.</para>
/// </summary>
public sealed class HardwareInfo
{
    /// <summary>Mac のモデル識別子 (hw.model)。例: "Mac14,12"<br/>Mac model identifier (hw.model). Example: "Mac14,12"</summary>
    public string Model { get; }

    /// <summary>CPU アーキテクチャ (hw.machine)。例: "arm64"<br/>CPU architecture (hw.machine). Example: "arm64"</summary>
    public string Machine { get; }

    /// <summary>ボード識別子 (hw.targettype)。例: "J474s"。取得できない場合は null<br/>Board identifier (hw.targettype). Example: "J474s". Returns null if unavailable.</summary>
    public string? TargetType { get; }

    /// <summary>CPU のブランド名文字列 (machdep.cpu.brand_string)。例: "Apple M2 Pro"。取得できない場合は null<br/>CPU brand string (machdep.cpu.brand_string). Example: "Apple M2 Pro". Returns null if unavailable.</summary>
    public string? CpuBrandString { get; }

    /// <summary>Mac のシリアル番号 (IOPlatformExpertDevice)。取得できない場合は null<br/>Mac serial number from IOPlatformExpertDevice. Returns null if unavailable.</summary>
    public string? SerialNumber { get; }

    /// <summary>現在アクティブな論理 CPU 数 (hw.logicalcpu)<br/>Number of currently active logical CPUs (hw.logicalcpu)</summary>
    public int LogicalCpu { get; }

    /// <summary>論理 CPU の最大数 (hw.logicalcpu_max)<br/>Maximum number of logical CPUs (hw.logicalcpu_max)</summary>
    public int LogicalCpuMax { get; }

    /// <summary>現在アクティブな物理 CPU 数 (hw.physicalcpu)<br/>Number of currently active physical CPUs (hw.physicalcpu)</summary>
    public int PhysicalCpu { get; }

    /// <summary>物理 CPU の最大数 (hw.physicalcpu_max)<br/>Maximum number of physical CPUs (hw.physicalcpu_max)</summary>
    public int PhysicalCpuMax { get; }

    /// <summary>アクティブな CPU 数 (hw.activecpu)。省電力モードで変動する場合がある<br/>Number of active CPUs (hw.activecpu). May vary in power-saving mode.</summary>
    public int ActiveCpu { get; }

    /// <summary>CPU 数 (hw.ncpu)。通常は LogicalCpu と同値<br/>CPU count (hw.ncpu). Typically equals LogicalCpu.</summary>
    public int Ncpu { get; }

    /// <summary>CPU コア数 (machdep.cpu.core_count)。Apple Silicon で取得可<br/>CPU core count (machdep.cpu.core_count). Available on Apple Silicon.</summary>
    public int CpuCoreCount { get; }

    /// <summary>CPU スレッド数 (machdep.cpu.thread_count)。Apple Silicon で取得可<br/>CPU thread count (machdep.cpu.thread_count). Available on Apple Silicon.</summary>
    public int CpuThreadCount { get; }

    /// <summary>タイムベース周波数 (Hz) (hw.tbfrequency)。Mach 絶対時間の基準周波数<br/>Timebase frequency in Hz (hw.tbfrequency). Reference frequency for Mach absolute time.</summary>
    public long TbFrequency { get; }

    /// <summary>物理メモリの総量 (バイト) (hw.memsize)<br/>Total physical memory in bytes (hw.memsize)</summary>
    public long MemSize { get; }

    /// <summary>ページサイズ (バイト) (hw.pagesize)<br/>Memory page size in bytes (hw.pagesize)</summary>
    public long PageSize { get; }

    /// <summary>バイトオーダー (hw.byteorder)。1234=リトルエンディアン、4321=ビッグエンディアン<br/>Byte order (hw.byteorder). 1234=little-endian, 4321=big-endian</summary>
    public int ByteOrder { get; }

    /// <summary>キャッシュラインサイズ (バイト) (hw.cachelinesize)<br/>Cache line size in bytes (hw.cachelinesize)</summary>
    public long CacheLineSize { get; }

    /// <summary>L1 命令キャッシュサイズ (バイト) (hw.l1icachesize)<br/>L1 instruction cache size in bytes (hw.l1icachesize)</summary>
    public long L1ICacheSize { get; }

    /// <summary>L1 データキャッシュサイズ (バイト) (hw.l1dcachesize)<br/>L1 data cache size in bytes (hw.l1dcachesize)</summary>
    public long L1DCacheSize { get; }

    /// <summary>L2 キャッシュサイズ (バイト) (hw.l2cachesize)<br/>L2 cache size in bytes (hw.l2cachesize)</summary>
    public long L2CacheSize { get; }

    /// <summary>L3 キャッシュサイズ (バイト) (hw.l3cachesize)。プロセッサにより存在しない場合は 0<br/>L3 cache size in bytes (hw.l3cachesize). Returns 0 if the processor has no L3 cache.</summary>
    public long L3CacheSize { get; }

    /// <summary>物理 CPU パッケージ数 (hw.packages)<br/>Number of physical CPU packages (hw.packages)</summary>
    public int Packages { get; }

    /// <summary>64 ビット対応かどうか (hw.cpu64bit_capable)<br/>Whether the CPU supports 64-bit execution (hw.cpu64bit_capable)</summary>
    public bool Cpu64BitCapable { get; }

    /// <summary>パフォーマンスコア (P-core) の論理コア数 (hw.perflevel0.logicalcpu)。Apple Silicon 以外では 0。0 の場合 PerformanceCoreLevel はサポートされない<br/>Number of logical performance cores / P-cores (hw.perflevel0.logicalcpu). Returns 0 on non-Apple Silicon. When 0, PerformanceCoreLevel is unsupported.</summary>
    public int PerformanceCoreCount { get; }

    /// <summary>効率コア (E-core) の論理コア数 (hw.perflevel1.logicalcpu)。Apple Silicon 以外では 0。0 の場合 EfficiencyCoreLevel はサポートされない<br/>Number of logical efficiency cores / E-cores (hw.perflevel1.logicalcpu). Returns 0 on non-Apple Silicon. When 0, EfficiencyCoreLevel is unsupported.</summary>
    public int EfficiencyCoreCount { get; }

    /// <summary>パフォーマンスコア (P-core) のパフォーマンスレベル情報。PerformanceCoreCount が 0 の場合はデフォルト値<br/>Performance core (P-core) level info. Default values when PerformanceCoreCount is 0.</summary>
    public CorePerformanceLevel PerformanceCoreLevel { get; }

    /// <summary>効率コア (E-core) のパフォーマンスレベル情報。EfficiencyCoreCount が 0 の場合はデフォルト値<br/>Efficiency core (E-core) level info. Default values when EfficiencyCoreCount is 0.</summary>
    public CorePerformanceLevel EfficiencyCoreLevel { get; }

    /// <summary>システムに搭載されているすべての GPU の静的ハードウェア情報<br/>Static hardware information for all GPUs in the system</summary>
    public IReadOnlyList<GpuInfo> Gpus { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private HardwareInfo()
    {
        Model = GetSystemControlString("hw.model") ?? string.Empty;
        Machine = GetSystemControlString("hw.machine") ?? string.Empty;
        TargetType = GetSystemControlString("hw.targettype");
        CpuBrandString = GetSystemControlString("machdep.cpu.brand_string");
        SerialNumber = GetSerialNumber();
        LogicalCpu = GetSystemControlInt32("hw.logicalcpu");
        LogicalCpuMax = GetSystemControlInt32("hw.logicalcpu_max");
        PhysicalCpu = GetSystemControlInt32("hw.physicalcpu");
        PhysicalCpuMax = GetSystemControlInt32("hw.physicalcpu_max");
        ActiveCpu = GetSystemControlInt32("hw.activecpu");
        Ncpu = GetSystemControlInt32("hw.ncpu");
        CpuCoreCount = GetSystemControlInt32("machdep.cpu.core_count");
        CpuThreadCount = GetSystemControlInt32("machdep.cpu.thread_count");
        TbFrequency = GetSystemControlInt64("hw.tbfrequency");
        MemSize = GetSystemControlInt64("hw.memsize");
        PageSize = GetSystemControlInt64("hw.pagesize");
        ByteOrder = GetSystemControlInt32("hw.byteorder");
        CacheLineSize = GetSystemControlInt64("hw.cachelinesize");
        L1ICacheSize = GetSystemControlInt64("hw.l1icachesize");
        L1DCacheSize = GetSystemControlInt64("hw.l1dcachesize");
        L2CacheSize = GetSystemControlInt64("hw.l2cachesize");
        L3CacheSize = GetSystemControlInt64("hw.l3cachesize");
        Packages = GetSystemControlInt32("hw.packages");
        Cpu64BitCapable = GetSystemControlInt32("hw.cpu64bit_capable") != 0;
        var nperflevels = GetSystemControlInt32("hw.nperflevels");
        PerformanceCoreCount = nperflevels > 0 ? GetSystemControlInt32("hw.perflevel0.logicalcpu") : 0;
        EfficiencyCoreCount = nperflevels > 1 ? GetSystemControlInt32("hw.perflevel1.logicalcpu") : 0;
        PerformanceCoreLevel = nperflevels > 0 ? ReadCorePerformanceLevel(0) : new CorePerformanceLevel();
        EfficiencyCoreLevel = nperflevels > 1 ? ReadCorePerformanceLevel(1) : new CorePerformanceLevel();
        Gpus = ReadGpus();
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    /// <summary>ハードウェア情報スナップショットを生成する。<br/>Creates a snapshot of hardware information.</summary>
    public static HardwareInfo Create() => new();

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
            CoreType = name switch
            {
                "Performance" => CpuCoreType.Performance,
                "Efficiency" => CpuCoreType.Efficiency,
                _ => CpuCoreType.Unknown
            },
            Index = index,
            Name = name,
            PhysicalCpu = GetSystemControlInt32($"hw.perflevel{index}.physicalcpu"),
            LogicalCpu = GetSystemControlInt32($"hw.perflevel{index}.logicalcpu"),
            CpusPerL2 = GetSystemControlInt32($"hw.perflevel{index}.cpusperl2"),
            L2CacheSize = GetSystemControlInt32($"hw.perflevel{index}.l2cachesize"),
            CpuFrequencyMax = GetSystemControlInt32($"hw.perflevel{index}.cpufreq_max"),
        };
    }

    private static GpuInfo[] ReadGpus()
    {
        var iterPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref iterPtr);
        if (kr != KERN_SUCCESS || iterPtr == IntPtr.Zero)
        {
            return [];
        }

        using var iter = new IORef(iterPtr);
        var results = new List<GpuInfo>();
        uint raw;
        while ((raw = IOIteratorNext(iter)) != 0)
        {
            using var entry = new IOObj(raw);

            var info = new GpuInfo
            {
                Model = entry.GetString("model") ?? "(unknown)",
                Name = entry.GetString("IOClass") ?? "(unknown)",
                MetalPluginName = entry.GetString("MetalPluginName") ?? string.Empty,
                CoreCount = (int)entry.GetUInt64("gpu-core-count"),
                VendorId = entry.GetDataUInt32("vendor-id"),
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
                    UscGeneration = (int)dict.GetInt64("usc_gen"),
                };
            }

            results.Add(info);
        }

        return [.. results];
    }
}
