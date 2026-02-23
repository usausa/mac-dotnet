namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// GPU の静的なハードウェア構成情報 (GPUConfigurationVariable)。
/// <para>Static GPU hardware configuration information (GPUConfigurationVariable).</para>
/// </summary>
public sealed record GpuConfiguration
{
    /// <summary>GPU アーキテクチャ世代番号<br/>GPU architecture generation number</summary>
    public required int GpuGeneration { get; init; }

    /// <summary>GPU コア (Execution Unit) の数<br/>Number of GPU cores (Execution Units)</summary>
    public required int NumCores { get; init; }

    /// <summary>ジオメトリプロセッサ (GP) の数<br/>Number of geometry processors (GPs)</summary>
    public required int NumGPs { get; init; }

    /// <summary>フラグメントシェーダーユニットの数<br/>Number of fragment shader units</summary>
    public required int NumFragments { get; init; }

    /// <summary>マルチ GPU の数 (通常は 1)<br/>Number of multi-GPU units (typically 1)</summary>
    public required int NumMGpus { get; init; }

    /// <summary>USC (Unified Shader Core) のアーキテクチャ世代番号<br/>USC (Unified Shader Core) architecture generation number</summary>
    public required int UscGeneration { get; init; }
}

/// <summary>
/// GPU の静的なハードウェア識別情報。HardwareInfo.GetGpus() で取得する。
/// <para>Static GPU hardware identification info. Retrieve via HardwareInfo.GetGpus().</para>
/// </summary>
public sealed record GpuInfo
{
    /// <summary>GPU モデル名。例: "Apple M2 Pro"<br/>GPU model name. Example: "Apple M2 Pro"</summary>
    public required string Model { get; init; }

    /// <summary>IOKit のクラス名。GpuDevice.Name と対応する。例: "AGXAcceleratorG14X"<br/>IOKit class name. Matches GpuDevice.Name. Example: "AGXAcceleratorG14X"</summary>
    public required string ClassName { get; init; }

    /// <summary>Metal プラグイン名。例: "AGXMetalG14X"。取得できない場合は null<br/>Metal plugin name. Example: "AGXMetalG14X". Returns null if unavailable.</summary>
    public string? MetalPluginName { get; init; }

    /// <summary>GPU コア数<br/>Number of GPU cores</summary>
    public required int CoreCount { get; init; }

    /// <summary>GPU ベンダー ID。例: Apple Silicon の場合は 0x106B<br/>GPU vendor ID. Example: 0x106B for Apple Silicon</summary>
    public required uint VendorId { get; init; }

    /// <summary>GPU 構成情報。GPUConfigurationVariable が存在する場合のみ非 null<br/>GPU configuration info. Non-null only when GPUConfigurationVariable is present.</summary>
    public GpuConfiguration? Configuration { get; init; }
}

/// <summary>
/// CPU パフォーマンスレベルのエントリ (P-core / E-core)。
/// <para>CPU performance level entry (P-core / E-core). Apple Silicon only.</para>
/// </summary>
public sealed record PerformanceLevelEntry
{
    /// <summary>パフォーマンスレベルのインデックス。0 が最高性能 (P-core)、1 以降が省電力 (E-core)<br/>Performance level index. 0 = highest performance (P-core), 1+ = efficiency (E-core)</summary>
    public required int Index { get; init; }

    /// <summary>レベル名 (hw.perflevelN.name)。例: "Performance"、"Efficiency"<br/>Level name (hw.perflevelN.name). Example: "Performance", "Efficiency"</summary>
    public required string Name { get; init; }

    /// <summary>このレベルに属する物理コア数 (hw.perflevelN.physicalcpu)<br/>Number of physical CPUs at this level (hw.perflevelN.physicalcpu)</summary>
    public required int PhysicalCpu { get; init; }

    /// <summary>このレベルに属する論理コア数 (hw.perflevelN.logicalcpu)<br/>Number of logical CPUs at this level (hw.perflevelN.logicalcpu)</summary>
    public required int LogicalCpu { get; init; }

    /// <summary>L2 キャッシュを共有する CPU の数 (hw.perflevelN.cpusperl2)<br/>Number of CPUs sharing an L2 cache (hw.perflevelN.cpusperl2)</summary>
    public required int CpusPerL2 { get; init; }

    /// <summary>L2 キャッシュサイズ (バイト) (hw.perflevelN.l2cachesize)<br/>L2 cache size in bytes (hw.perflevelN.l2cachesize)</summary>
    public required int L2CacheSize { get; init; }

    /// <summary>このレベルの CPU 最大周波数 (Hz) (hw.perflevelN.cpufreq_max)。Apple Silicon では取得できない場合は 0<br/>Maximum CPU frequency in Hz (hw.perflevelN.cpufreq_max). Returns 0 if unavailable on Apple Silicon.</summary>
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

    /// <summary>CPU アーキテクチャ (hw.machine)。例: "arm64"、"x86_64"<br/>CPU architecture (hw.machine). Example: "arm64", "x86_64"</summary>
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

    /// <summary>CPU 周波数 (Hz) (hw.cpufrequency)。Apple Silicon では取得できない場合は 0<br/>CPU frequency in Hz (hw.cpufrequency). Returns 0 if unavailable on Apple Silicon.</summary>
    public long CpuFrequency { get; }

    /// <summary>CPU 最大周波数 (Hz) (hw.cpufrequency_max)<br/>Maximum CPU frequency in Hz (hw.cpufrequency_max)</summary>
    public long CpuFrequencyMax { get; }

    /// <summary>バス周波数 (Hz) (hw.busfrequency)。Apple Silicon では取得できない場合は 0<br/>Bus frequency in Hz (hw.busfrequency). Returns 0 if unavailable on Apple Silicon.</summary>
    public long BusFrequency { get; }

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

    /// <summary>パフォーマンスコア (P-core) の論理コア数 (hw.perflevel0.logicalcpu)。Apple Silicon 以外では 0<br/>Number of logical performance cores / P-cores (hw.perflevel0.logicalcpu). Returns 0 on non-Apple Silicon.</summary>
    public int PCoreCount { get; }

    /// <summary>効率コア (E-core) の論理コア数 (hw.perflevel1.logicalcpu)。Apple Silicon 以外では 0<br/>Number of logical efficiency cores / E-cores (hw.perflevel1.logicalcpu). Returns 0 on non-Apple Silicon.</summary>
    public int ECoreCount { get; }

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
        CpuFrequency = GetSystemControlInt64("hw.cpufrequency");
        CpuFrequencyMax = GetSystemControlInt64("hw.cpufrequency_max");
        BusFrequency = GetSystemControlInt64("hw.busfrequency");
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
        PCoreCount = nperflevels > 0 ? GetSystemControlInt32("hw.perflevel0.logicalcpu") : 0;
        ECoreCount = nperflevels > 1 ? GetSystemControlInt32("hw.perflevel1.logicalcpu") : 0;
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    /// <summary>ハードウェア情報スナップショットを生成する。<br/>Creates a snapshot of hardware information.</summary>
    public static HardwareInfo Create() => new();

    /// <summary>システムに搭載されているすべての GPU の静的ハードウェア情報を返す。<br/>Returns static hardware information for all GPUs in the system.</summary>
    public static GpuInfo[] GetGpus()
    {
        var iter = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref iter);
        if (kr != KERN_SUCCESS || iter == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var results = new List<GpuInfo>();
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    results.Add(ReadGpuInfo(entry));
                }
                finally
                {
                    IOObjectRelease(entry);
                }
            }

            return [.. results];
        }
        finally
        {
            IOObjectRelease(iter);
        }
    }

    private static GpuInfo ReadGpuInfo(uint entry)
    {
        return new GpuInfo
        {
            Model = GetIokitString(entry, "model") ?? "(unknown)",
            ClassName = GetIokitString(entry, "IOClass") ?? "(unknown)",
            MetalPluginName = GetIokitString(entry, "MetalPluginName"),
            CoreCount = (int)GetIokitNumber(entry, "gpu-core-count"),
            VendorId = GetIokitDataUInt32LE(entry, "vendor-id"),
            Configuration = ReadGpuConfiguration(entry),
        };
    }

    private static GpuConfiguration? ReadGpuConfiguration(uint entry)
    {
        var dict = GetIokitDictionary(entry, "GPUConfigurationVariable");
        if (dict == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return new GpuConfiguration
            {
                GpuGeneration = (int)GetIokitDictNumber(dict, "gpu_gen"),
                NumCores = (int)GetIokitDictNumber(dict, "num_cores"),
                NumGPs = (int)GetIokitDictNumber(dict, "num_gps"),
                NumFragments = (int)GetIokitDictNumber(dict, "num_frags"),
                NumMGpus = (int)GetIokitDictNumber(dict, "num_mgpus"),
                UscGeneration = (int)GetIokitDictNumber(dict, "usc_gen"),
            };
        }
        finally
        {
            CFRelease(dict);
        }
    }

    /// <summary>
    /// CPU パフォーマンスレベル (P-core / E-core) の一覧を返す。Apple Silicon 以外では空配列。
    /// <para>Returns the list of CPU performance levels (P-core / E-core). Returns empty array on non-Apple Silicon.</para>
    /// </summary>
    public static PerformanceLevelEntry[] GetPerformanceLevels()
    {
        var count = GetSystemControlInt32("hw.nperflevels");
        if (count <= 0)
        {
            return [];
        }

        var levels = new PerformanceLevelEntry[count];
        for (var i = 0; i < count; i++)
        {
            levels[i] = new PerformanceLevelEntry
            {
                Index = i,
                Name = GetSystemControlString($"hw.perflevel{i}.name") ?? $"Level {i}",
                PhysicalCpu = GetSystemControlInt32($"hw.perflevel{i}.physicalcpu"),
                LogicalCpu = GetSystemControlInt32($"hw.perflevel{i}.logicalcpu"),
                CpusPerL2 = GetSystemControlInt32($"hw.perflevel{i}.cpusperl2"),
                L2CacheSize = GetSystemControlInt32($"hw.perflevel{i}.l2cachesize"),
                CpuFrequencyMax = GetSystemControlInt32($"hw.perflevel{i}.cpufreq_max"),
            };
        }

        return levels;
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

        var service = IOServiceGetMatchingService(0, matching);
        if (service == 0)
        {
            return null;
        }

        try
        {
            var key = CFStringCreateWithCString(IntPtr.Zero, "IOPlatformSerialNumber", kCFStringEncodingUTF8);
            var value = IORegistryEntryCreateCFProperty(service, key, IntPtr.Zero, 0);
            CFRelease(key);

            if (value == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return CfStringToManaged(value);
            }
            finally
            {
                CFRelease(value);
            }
        }
        finally
        {
            IOObjectRelease(service);
        }
    }
}
