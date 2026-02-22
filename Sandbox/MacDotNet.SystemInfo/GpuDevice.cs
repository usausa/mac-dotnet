namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// 1 台の GPU デバイスの動的な統計・センサー情報を管理するクラス。
/// LinuxDotNet.SystemInfo の CpuCore と同じパターンで、Update() を呼ぶたびに最新値を取得する。
/// 静的なハードウェア情報 (モデル名・コア数・ベンダー ID 等) は HardwareInfo.GetGpus() を使用する。
/// Name と GpuInfo.ClassName が一致するエントリが同一デバイスに対応する。
/// </summary>
public sealed class GpuDevice
{
    /// <summary>IOKit クラス名。HardwareInfo.GetGpus() の GpuInfo.ClassName と対応する。例: "AGXAcceleratorG14X"</summary>
    public string Name { get; }

    /// <summary>最後に Update() を呼び出した日時</summary>
    public DateTime UpdateAt { get; private set; }

    //--------------------------------------------------------------------------------
    // PerformanceStatistics
    //--------------------------------------------------------------------------------

    /// <summary>GPU 全体の使用率 (%)。取得できない場合は 0</summary>
    public long DeviceUtilization { get; private set; }

    /// <summary>レンダリングパイプラインの使用率 (%)。取得できない場合は 0</summary>
    public long RendererUtilization { get; private set; }

    /// <summary>タイラー (ジオメトリ処理) の使用率 (%)。取得できない場合は 0</summary>
    public long TilerUtilization { get; private set; }

    /// <summary>GPU がシステムメモリに確保した総メモリ量 (バイト)。取得できない場合は 0</summary>
    public long AllocSystemMemory { get; private set; }

    /// <summary>GPU が現在使用中のシステムメモリ量 (バイト)。取得できない場合は 0</summary>
    public long InUseSystemMemory { get; private set; }

    /// <summary>ドライバが使用中のシステムメモリ量 (バイト)。取得できない場合は 0</summary>
    public long InUseSystemMemoryDriver { get; private set; }

    /// <summary>タイル描画に使用されたシーンデータの総バイト数。取得できない場合は 0</summary>
    public long TiledSceneBytes { get; private set; }

    /// <summary>パラメータバッファ (PB) に割り当てられたサイズ (バイト)。取得できない場合は 0</summary>
    public long AllocatedPBSize { get; private set; }

    /// <summary>GPU リセット (リカバリー) の発生回数 (累積値)。取得できない場合は 0</summary>
    public long RecoveryCount { get; private set; }

    /// <summary>シーン分割処理が発生した回数 (累積値)。取得できない場合は 0</summary>
    public long SplitSceneCount { get; private set; }

    //--------------------------------------------------------------------------------
    // Sensor / Hardware Monitor
    //--------------------------------------------------------------------------------

    /// <summary>GPU 温度 (°C)。取得できない場合は null</summary>
    public int? Temperature { get; private set; }

    /// <summary>ファン速度 (%)。取得できない場合は null</summary>
    public int? FanSpeed { get; private set; }

    /// <summary>コアクロック周波数 (MHz)。取得できない場合は null</summary>
    public int? CoreClock { get; private set; }

    /// <summary>メモリクロック周波数 (MHz)。取得できない場合は null</summary>
    public int? MemoryClock { get; private set; }

    /// <summary>GPU の電源状態。true = オン、false = AGC によりオフ、null = 不明</summary>
    public bool? PowerState { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private GpuDevice(string name)
    {
        Name = name;
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static GpuDevice[] GetDevices()
    {
        var iter = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref iter);
        if (kr != KERN_SUCCESS || iter == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var results = new List<GpuDevice>();
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    var name = GetIokitString(entry, "IOClass") ?? "(unknown)";
                    var device = new GpuDevice(name);
                    device.UpdateFromEntry(entry);
                    results.Add(device);
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

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        var iter = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref iter);
        if (kr != KERN_SUCCESS || iter == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    var ioClass = GetIokitString(entry, "IOClass");
                    if (string.Equals(ioClass, Name, StringComparison.Ordinal))
                    {
                        UpdateFromEntry(entry);
                        return true;
                    }
                }
                finally
                {
                    IOObjectRelease(entry);
                }
            }

            return false;
        }
        finally
        {
            IOObjectRelease(iter);
        }
    }

    //--------------------------------------------------------------------------------
    // Private helpers
    //--------------------------------------------------------------------------------

    private void UpdateFromEntry(uint entry)
    {
        int? temperature = null;
        int? fanSpeed = null;
        int? coreClock = null;
        int? memoryClock = null;
        bool? powerState = null;

        var perfDict = GetIokitDictionary(entry, "PerformanceStatistics");
        if (perfDict != IntPtr.Zero)
        {
            try
            {
                DeviceUtilization = GetIokitDictNumber(perfDict, "Device Utilization %");
                RendererUtilization = GetIokitDictNumber(perfDict, "Renderer Utilization %");
                TilerUtilization = GetIokitDictNumber(perfDict, "Tiler Utilization %");
                AllocSystemMemory = GetIokitDictNumber(perfDict, "Alloc system memory");
                InUseSystemMemory = GetIokitDictNumber(perfDict, "In use system memory");
                InUseSystemMemoryDriver = GetIokitDictNumber(perfDict, "In use system memory (driver)");
                TiledSceneBytes = GetIokitDictNumber(perfDict, "TiledSceneBytes");
                AllocatedPBSize = GetIokitDictNumber(perfDict, "Allocated PB Size");
                RecoveryCount = GetIokitDictNumber(perfDict, "recoveryCount");
                SplitSceneCount = GetIokitDictNumber(perfDict, "SplitSceneCount");

                var rawTemp = GetIokitDictNumber(perfDict, "Temperature(C)");
                var rawFan = GetIokitDictNumber(perfDict, "Fan Speed(%)");
                var rawCore = GetIokitDictNumber(perfDict, "Core Clock(MHz)");
                var rawMem = GetIokitDictNumber(perfDict, "Memory Clock(MHz)");

                temperature = rawTemp > 0 && rawTemp < 128 ? (int)rawTemp : null;
                fanSpeed = rawFan > 0 ? (int)rawFan : null;
                coreClock = rawCore > 0 ? (int)rawCore : null;
                memoryClock = rawMem > 0 ? (int)rawMem : null;
            }
            finally
            {
                CFRelease(perfDict);
            }
        }
        else
        {
            DeviceUtilization = 0;
            RendererUtilization = 0;
            TilerUtilization = 0;
            AllocSystemMemory = 0;
            InUseSystemMemory = 0;
            InUseSystemMemoryDriver = 0;
            TiledSceneBytes = 0;
            AllocatedPBSize = 0;
            RecoveryCount = 0;
            SplitSceneCount = 0;
        }

        var agcInfo = GetIokitDictionary(entry, "AGCInfo");
        if (agcInfo != IntPtr.Zero)
        {
            var poweredOff = GetIokitDictNumber(agcInfo, "poweredOffByAGC");
            powerState = poweredOff == 0;
            CFRelease(agcInfo);
        }

        if (temperature is null)
        {
            if (Name.Contains("intel", StringComparison.OrdinalIgnoreCase))
            {
                temperature = HardwareMonitor.ReadTemperatureOnce("TCGC");
            }
            else if (Name.Contains("amd", StringComparison.OrdinalIgnoreCase))
            {
                temperature = HardwareMonitor.ReadTemperatureOnce("TGDD");
            }
        }

        Temperature = temperature;
        FanSpeed = fanSpeed;
        CoreClock = coreClock;
        MemoryClock = memoryClock;
        PowerState = powerState;
        UpdateAt = DateTime.Now;
    }
}
