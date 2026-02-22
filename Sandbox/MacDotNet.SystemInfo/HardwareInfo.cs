namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>GPU の静的なハードウェア構成情報 (GPUConfigurationVariable)</summary>
public sealed record GpuConfiguration
{
    /// <summary>GPU アーキテクチャ世代番号</summary>
    public required int GpuGeneration { get; init; }

    /// <summary>GPU コア (Execution Unit) の数</summary>
    public required int NumCores { get; init; }

    /// <summary>ジオメトリプロセッサ (GP) の数</summary>
    public required int NumGPs { get; init; }

    /// <summary>フラグメントシェーダーユニットの数</summary>
    public required int NumFragments { get; init; }

    /// <summary>マルチ GPU の数 (通常は 1)</summary>
    public required int NumMGpus { get; init; }

    /// <summary>USC (Unified Shader Core) のアーキテクチャ世代番号</summary>
    public required int UscGeneration { get; init; }
}

/// <summary>GPU の静的なハードウェア識別情報。HardwareInfo.GetGpus() で取得する</summary>
public sealed record GpuInfo
{
    /// <summary>GPU モデル名。例: "Apple M2 Pro"</summary>
    public required string Model { get; init; }

    /// <summary>IOKit のクラス名。GpuDevice.Name と対応する。例: "AGXAcceleratorG14X"</summary>
    public required string ClassName { get; init; }

    /// <summary>Metal プラグイン名。例: "AGXMetalG14X"。取得できない場合は null</summary>
    public string? MetalPluginName { get; init; }

    /// <summary>GPU コア数</summary>
    public required int CoreCount { get; init; }

    /// <summary>GPU ベンダー ID。例: Apple Silicon の場合は 0x106B</summary>
    public required uint VendorId { get; init; }

    /// <summary>GPU 構成情報。GPUConfigurationVariable が存在する場合のみ非 null</summary>
    public GpuConfiguration? Configuration { get; init; }
}

public sealed record PerformanceLevelEntry
{
    /// <summary>パフォーマンスレベルのインデックス。0 が最高性能 (P-core)、1 以降が省電力 (E-core)</summary>
    public required int Index { get; init; }

    /// <summary>レベル名 (hw.perflevelN.name)。例: "Performance"、"Efficiency"</summary>
    public required string Name { get; init; }

    /// <summary>このレベルに属する物理コア数 (hw.perflevelN.physicalcpu)</summary>
    public required int PhysicalCpu { get; init; }

    /// <summary>このレベルに属する論理コア数 (hw.perflevelN.logicalcpu)</summary>
    public required int LogicalCpu { get; init; }

    /// <summary>L2 キャッシュを共有する CPU の数 (hw.perflevelN.cpusperl2)</summary>
    public required int CpusPerL2 { get; init; }

    /// <summary>L2 キャッシュサイズ (バイト) (hw.perflevelN.l2cachesize)</summary>
    public required int L2CacheSize { get; init; }

    /// <summary>このレベルの CPU 最大周波数 (Hz) (hw.perflevelN.cpufreq_max)。Apple Silicon では取得できない場合は 0</summary>
    public int CpuFrequencyMax { get; init; }
}

public sealed class HardwareInfo
{
    /// <summary>Mac のモデル識別子 (hw.model)。例: "Mac14,12"</summary>
    public string Model { get; }

    /// <summary>CPU アーキテクチャ (hw.machine)。例: "arm64"、"x86_64"</summary>
    public string Machine { get; }

    /// <summary>ボード識別子 (hw.targettype)。例: "J474s"。取得できない場合は null</summary>
    public string? TargetType { get; }

    /// <summary>CPU のブランド名文字列 (machdep.cpu.brand_string)。例: "Apple M2 Pro"。取得できない場合は null</summary>
    public string? CpuBrandString { get; }

    /// <summary>Mac のシリアル番号 (IOPlatformExpertDevice)。取得できない場合は null</summary>
    public string? SerialNumber { get; }

    /// <summary>現在アクティブな論理 CPU 数 (hw.logicalcpu)</summary>
    public int LogicalCpu { get; }

    /// <summary>論理 CPU の最大数 (hw.logicalcpu_max)</summary>
    public int LogicalCpuMax { get; }

    /// <summary>現在アクティブな物理 CPU 数 (hw.physicalcpu)</summary>
    public int PhysicalCpu { get; }

    /// <summary>物理 CPU の最大数 (hw.physicalcpu_max)</summary>
    public int PhysicalCpuMax { get; }

    /// <summary>アクティブな CPU 数 (hw.activecpu)。省電力モードで変動する場合がある</summary>
    public int ActiveCpu { get; }

    /// <summary>CPU 数 (hw.ncpu)。通常は LogicalCpu と同値</summary>
    public int Ncpu { get; }

    /// <summary>CPU コア数 (machdep.cpu.core_count)。Apple Silicon で取得可</summary>
    public int CpuCoreCount { get; }

    /// <summary>CPU スレッド数 (machdep.cpu.thread_count)。Apple Silicon で取得可</summary>
    public int CpuThreadCount { get; }

    /// <summary>CPU 周波数 (Hz) (hw.cpufrequency)。Apple Silicon では取得できない場合は 0</summary>
    public long CpuFrequency { get; }

    /// <summary>CPU 最大周波数 (Hz) (hw.cpufrequency_max)</summary>
    public long CpuFrequencyMax { get; }

    /// <summary>バス周波数 (Hz) (hw.busfrequency)。Apple Silicon では取得できない場合は 0</summary>
    public long BusFrequency { get; }

    /// <summary>タイムベース周波数 (Hz) (hw.tbfrequency)。Mach 絶対時間の基準周波数</summary>
    public long TbFrequency { get; }

    /// <summary>物理メモリの総量 (バイト) (hw.memsize)</summary>
    public long MemSize { get; }

    /// <summary>ページサイズ (バイト) (hw.pagesize)</summary>
    public long PageSize { get; }

    /// <summary>バイトオーダー (hw.byteorder)。1234=リトルエンディアン、4321=ビッグエンディアン</summary>
    public int ByteOrder { get; }

    /// <summary>キャッシュラインサイズ (バイト) (hw.cachelinesize)</summary>
    public long CacheLineSize { get; }

    /// <summary>L1 命令キャッシュサイズ (バイト) (hw.l1icachesize)</summary>
    public long L1ICacheSize { get; }

    /// <summary>L1 データキャッシュサイズ (バイト) (hw.l1dcachesize)</summary>
    public long L1DCacheSize { get; }

    /// <summary>L2 キャッシュサイズ (バイト) (hw.l2cachesize)</summary>
    public long L2CacheSize { get; }

    /// <summary>L3 キャッシュサイズ (バイト) (hw.l3cachesize)。プロセッサにより存在しない場合は 0</summary>
    public long L3CacheSize { get; }

    /// <summary>物理 CPU パッケージ数 (hw.packages)</summary>
    public int Packages { get; }

    /// <summary>64 ビット対応かどうか (hw.cpu64bit_capable)</summary>
    public bool Cpu64BitCapable { get; }

    /// <summary>パフォーマンスコア (P-core) の論理コア数 (hw.perflevel0.logicalcpu)。Apple Silicon 以外では 0</summary>
    public int PCoreCount { get; }

    /// <summary>効率コア (E-core) の論理コア数 (hw.perflevel1.logicalcpu)。Apple Silicon 以外では 0</summary>
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

    public static HardwareInfo Create() => new();

    /// <summary>システムに搭載されているすべての GPU の静的ハードウェア情報を返す</summary>
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
