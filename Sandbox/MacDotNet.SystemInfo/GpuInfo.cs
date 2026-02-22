namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed record GpuPerformanceStatistics
{
    /// <summary>GPU 全体の使用率 (%)</summary>
    public required long DeviceUtilization { get; init; }

    /// <summary>レンダリングパイプラインの使用率 (%)</summary>
    public required long RendererUtilization { get; init; }

    /// <summary>タイラー (ジオメトリ処理) の使用率 (%)</summary>
    public required long TilerUtilization { get; init; }

    /// <summary>GPU がシステムメモリに確保した総メモリ量 (バイト)</summary>
    public required long AllocSystemMemory { get; init; }

    /// <summary>GPU が現在使用中のシステムメモリ量 (バイト)</summary>
    public required long InUseSystemMemory { get; init; }

    /// <summary>ドライバが使用中のシステムメモリ量 (バイト)</summary>
    public required long InUseSystemMemoryDriver { get; init; }

    /// <summary>タイル描画に使用されたシーンデータの総バイト数</summary>
    public required long TiledSceneBytes { get; init; }

    /// <summary>パラメータバッファ (PB) に割り当てられたサイズ (バイト)</summary>
    public required long AllocatedPBSize { get; init; }

    /// <summary>GPU リセット (リカバリー) の発生回数</summary>
    public required long RecoveryCount { get; init; }

    /// <summary>シーン分割処理が発生した回数</summary>
    public required long SplitSceneCount { get; init; }

    /// <summary>GPU 温度 (°C)。取得できない場合は 0</summary>
    public long Temperature { get; init; }

    /// <summary>ファン速度 (%)。取得できない場合は 0</summary>
    public long FanSpeed { get; init; }

    /// <summary>コアクロック周波数 (MHz)。取得できない場合は 0</summary>
    public long CoreClock { get; init; }

    /// <summary>メモリクロック周波数 (MHz)。取得できない場合は 0</summary>
    public long MemoryClock { get; init; }
}

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

public sealed record GpuEntry
{
    /// <summary>GPU モデル名。例: "Apple M2 Pro"</summary>
    public required string Model { get; init; }

    /// <summary>IOKit のクラス名。例: "AGXAcceleratorG14X"</summary>
    public required string ClassName { get; init; }

    /// <summary>Metal プラグイン名。例: "AGXMetalG14X"。取得できない場合は null</summary>
    public string? MetalPluginName { get; init; }

    /// <summary>GPU コア数</summary>
    public required int CoreCount { get; init; }

    /// <summary>GPU ベンダー ID。例: Apple Silicon の場合は 0x106B</summary>
    public required uint VendorId { get; init; }

    /// <summary>GPU 温度 (°C)。取得できない場合は null</summary>
    public int? Temperature { get; init; }

    /// <summary>ファン速度 (%)。取得できない場合は null</summary>
    public int? FanSpeed { get; init; }

    /// <summary>コアクロック周波数 (MHz)。取得できない場合は null</summary>
    public int? CoreClock { get; init; }

    /// <summary>メモリクロック周波数 (MHz)。取得できない場合は null</summary>
    public int? MemoryClock { get; init; }

    /// <summary>GPU の電源状態。true = オン、false = AGC によりオフ、null = 不明</summary>
    public bool? PowerState { get; init; }

    /// <summary>GPU パフォーマンス統計情報。IOAccelerator から取得できない場合は null</summary>
    public GpuPerformanceStatistics? Performance { get; init; }

    /// <summary>GPU 構成情報。GPUConfigurationVariable が存在する場合のみ非 null</summary>
    public GpuConfiguration? Configuration { get; init; }
}

public static class GpuInfo
{
    public static GpuEntry[] GetGpus()
    {
        var iter = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref iter);
        if (kr != KERN_SUCCESS || iter == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var results = new List<GpuEntry>();
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    results.Add(ReadGpuEntry(entry));
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

    private static GpuEntry ReadGpuEntry(uint entry)
    {
        var ioClass = GetStringProperty(entry, "IOClass");
        var performance = ReadPerformanceStatistics(entry);

        int? temperature = null;
        int? fanSpeed = null;
        int? coreClock = null;
        int? memoryClock = null;
        bool? powerState = null;

        if (performance is not null)
        {
            temperature = performance.Temperature > 0 && performance.Temperature < 128 ? (int)performance.Temperature : null;
            fanSpeed = performance.FanSpeed > 0 ? (int)performance.FanSpeed : null;
            coreClock = performance.CoreClock > 0 ? (int)performance.CoreClock : null;
            memoryClock = performance.MemoryClock > 0 ? (int)performance.MemoryClock : null;
        }

        var agcInfo = GetDictionaryProperty(entry, "AGCInfo");
        if (agcInfo != IntPtr.Zero)
        {
            var poweredOff = GetDictNumber(agcInfo, "poweredOffByAGC");
            powerState = poweredOff == 0;
            CFRelease(agcInfo);
        }

        if (temperature is null)
        {
            if (ioClass?.Contains("intel", StringComparison.OrdinalIgnoreCase) == true)
            {
                temperature = SmcInfo.ReadSmcTemperature("TCGC");
            }
            else if (ioClass?.Contains("amd", StringComparison.OrdinalIgnoreCase) == true)
            {
                temperature = SmcInfo.ReadSmcTemperature("TGDD");
            }
        }

        return new GpuEntry
        {
            Model = GetStringProperty(entry, "model") ?? "(unknown)",
            ClassName = ioClass ?? "(unknown)",
            MetalPluginName = GetStringProperty(entry, "MetalPluginName"),
            CoreCount = (int)GetNumberProperty(entry, "gpu-core-count"),
            VendorId = GetDataPropertyAsUInt32LE(entry, "vendor-id"),
            Temperature = temperature,
            FanSpeed = fanSpeed,
            CoreClock = coreClock,
            MemoryClock = memoryClock,
            PowerState = powerState,
            Performance = performance,
            Configuration = ReadGpuConfiguration(entry),
        };
    }

    private static GpuPerformanceStatistics? ReadPerformanceStatistics(uint entry)
    {
        var dict = GetDictionaryProperty(entry, "PerformanceStatistics");
        if (dict == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return new GpuPerformanceStatistics
            {
                DeviceUtilization = GetDictNumber(dict, "Device Utilization %"),
                RendererUtilization = GetDictNumber(dict, "Renderer Utilization %"),
                TilerUtilization = GetDictNumber(dict, "Tiler Utilization %"),
                AllocSystemMemory = GetDictNumber(dict, "Alloc system memory"),
                InUseSystemMemory = GetDictNumber(dict, "In use system memory"),
                InUseSystemMemoryDriver = GetDictNumber(dict, "In use system memory (driver)"),
                TiledSceneBytes = GetDictNumber(dict, "TiledSceneBytes"),
                AllocatedPBSize = GetDictNumber(dict, "Allocated PB Size"),
                RecoveryCount = GetDictNumber(dict, "recoveryCount"),
                SplitSceneCount = GetDictNumber(dict, "SplitSceneCount"),
                Temperature = GetDictNumber(dict, "Temperature(C)"),
                FanSpeed = GetDictNumber(dict, "Fan Speed(%)"),
                CoreClock = GetDictNumber(dict, "Core Clock(MHz)"),
                MemoryClock = GetDictNumber(dict, "Memory Clock(MHz)"),
            };
        }
        finally
        {
            CFRelease(dict);
        }
    }

    private static GpuConfiguration? ReadGpuConfiguration(uint entry)
    {
        var dict = GetDictionaryProperty(entry, "GPUConfigurationVariable");
        if (dict == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return new GpuConfiguration
            {
                GpuGeneration = (int)GetDictNumber(dict, "gpu_gen"),
                NumCores = (int)GetDictNumber(dict, "num_cores"),
                NumGPs = (int)GetDictNumber(dict, "num_gps"),
                NumFragments = (int)GetDictNumber(dict, "num_frags"),
                NumMGpus = (int)GetDictNumber(dict, "num_mgpus"),
                UscGeneration = (int)GetDictNumber(dict, "usc_gen"),
            };
        }
        finally
        {
            CFRelease(dict);
        }
    }

    private static unsafe string? GetStringProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                if (CFGetTypeID(val) != CFStringGetTypeID())
                {
                    return null;
                }

                return CfStringToManaged(val);
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static long GetNumberProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                if (CFGetTypeID(val) != CFNumberGetTypeID())
                {
                    return 0;
                }

                long result = 0;
                CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
                return result;
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static uint GetDataPropertyAsUInt32LE(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                if (CFGetTypeID(val) != CFDataGetTypeID())
                {
                    return 0;
                }

                var len = CFDataGetLength(val);
                if (len < 4)
                {
                    return 0;
                }

                var ptr = CFDataGetBytePtr(val);
                return (uint)(Marshal.ReadByte(ptr, 0)
                    | (Marshal.ReadByte(ptr, 1) << 8)
                    | (Marshal.ReadByte(ptr, 2) << 16)
                    | (Marshal.ReadByte(ptr, 3) << 24));
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static IntPtr GetDictionaryProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return IntPtr.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static long GetDictNumber(IntPtr dict, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            if (CFGetTypeID(val) != CFNumberGetTypeID())
            {
                return 0;
            }

            long result = 0;
            CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
            return result;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }
}
