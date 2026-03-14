namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// 1 台の GPU デバイスの動的な統計・センサー情報を管理するクラス。
/// LinuxDotNet.SystemInfo の CpuCore と同じパターンで、Update() を呼ぶたびに最新値を取得する。
/// 静的なハードウェア情報 (モデル名・コア数・ベンダー ID 等) は HardwareInfo.GetGpus() を使用する。
/// Name と GpuInfo.ClassName が一致するエントリが同一デバイスに対応する。
/// <para>
/// Manages dynamic performance statistics and sensor readings for a single GPU device.
/// Follows the same pattern as CpuCore: call Update() to refresh to the latest values.
/// For static hardware info (model, core count, vendor ID, etc.) use HardwareInfo.GetGpus().
/// Entries with matching Name and GpuInfo.ClassName represent the same physical device.
/// </para>
/// </summary>
public sealed class GpuDevice
{
    /// <summary>IOKit クラス名。HardwareInfo.GetGpus() の GpuInfo.ClassName と対応する。例: "AGXAcceleratorG14X"<br/>IOKit class name. Matches GpuInfo.ClassName from HardwareInfo.GetGpus(). Example: "AGXAcceleratorG14X"</summary>
    public string Name { get; }

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    //--------------------------------------------------------------------------------
    // PerformanceStatistics / パフォーマンス統計
    //--------------------------------------------------------------------------------

    /// <summary>GPU 全体の使用率 (%)。取得できない場合は 0<br/>Overall GPU utilization (%). Returns 0 if unavailable.</summary>
    public long DeviceUtilization { get; private set; }

    /// <summary>レンダリングパイプラインの使用率 (%)。取得できない場合は 0<br/>Renderer pipeline utilization (%). Returns 0 if unavailable.</summary>
    public long RendererUtilization { get; private set; }

    /// <summary>タイラー (ジオメトリ処理) の使用率 (%)。取得できない場合は 0<br/>Tiler (geometry processing) utilization (%). Returns 0 if unavailable.</summary>
    public long TilerUtilization { get; private set; }

    /// <summary>GPU がシステムメモリに確保した総メモリ量 (バイト)。取得できない場合は 0<br/>Total system memory allocated by the GPU in bytes. Returns 0 if unavailable.</summary>
    public long AllocSystemMemory { get; private set; }

    /// <summary>GPU が現在使用中のシステムメモリ量 (バイト)。取得できない場合は 0<br/>System memory currently in use by the GPU in bytes. Returns 0 if unavailable.</summary>
    public long InUseSystemMemory { get; private set; }

    /// <summary>ドライバが使用中のシステムメモリ量 (バイト)。取得できない場合は 0<br/>System memory in use by the GPU driver in bytes. Returns 0 if unavailable.</summary>
    public long InUseSystemMemoryDriver { get; private set; }

    /// <summary>タイル描画に使用されたシーンデータの総バイト数。取得できない場合は 0<br/>Total bytes of scene data used for tiled rendering. Returns 0 if unavailable.</summary>
    public long TiledSceneBytes { get; private set; }

    /// <summary>パラメータバッファ (PB) に割り当てられたサイズ (バイト)。取得できない場合は 0<br/>Size allocated for the parameter buffer (PB) in bytes. Returns 0 if unavailable.</summary>
    public long AllocatedPBSize { get; private set; }

    /// <summary>GPU リセット (リカバリー) の発生回数 (累積値)。取得できない場合は 0<br/>Cumulative GPU reset (recovery) count. Returns 0 if unavailable.</summary>
    public long RecoveryCount { get; private set; }

    /// <summary>シーン分割処理が発生した回数 (累積値)。取得できない場合は 0<br/>Cumulative scene split count. Returns 0 if unavailable.</summary>
    public long SplitSceneCount { get; private set; }

    //--------------------------------------------------------------------------------
    // Sensor / Hardware Monitor / センサー・ハードウェアモニター
    //--------------------------------------------------------------------------------

    /// <summary>GPU 温度 (°C)。取得できない場合は null<br/>GPU temperature in °C. Returns null if unavailable.</summary>
    public int? Temperature { get; private set; }

    /// <summary>ファン速度 (%)。取得できない場合は null<br/>Fan speed (%). Returns null if unavailable.</summary>
    public int? FanSpeed { get; private set; }

    /// <summary>コアクロック周波数 (MHz)。取得できない場合は null<br/>Core clock frequency in MHz. Returns null if unavailable.</summary>
    public int? CoreClock { get; private set; }

    /// <summary>メモリクロック周波数 (MHz)。取得できない場合は null<br/>Memory clock frequency in MHz. Returns null if unavailable.</summary>
    public int? MemoryClock { get; private set; }

    /// <summary>GPU の電源状態。true = オン、false = AGC によりオフ、null = 不明<br/>GPU power state. true = on, false = powered off by AGC, null = unknown.</summary>
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

    /// <summary>
    /// IOAccelerator サービス一覧を列挙して全 GPU デバイスのインスタンスを生成・返す。
    /// <para>Enumerates IOAccelerator services and returns instances for all GPU devices.</para>
    /// </summary>
    public static GpuDevice[] GetDevices()
    {
        var iterPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref iterPtr);
        if (kr != KERN_SUCCESS || iterPtr == IntPtr.Zero)
        {
            return [];
        }

        using var iter = new IORef(iterPtr);
        var results = new List<GpuDevice>();
        uint raw;
        while ((raw = IOIteratorNext(iter)) != 0)
        {
            using var entry = new IOObj(raw);
            var name = entry.GetString("IOClass") ?? "(unknown)";
            var device = new GpuDevice(name);
            device.UpdateFromEntry(entry);
            results.Add(device);
        }

        return [.. results];
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOKit から最新の GPU 統計・センサー情報を取得してプロパティを更新する。
    /// 対応するデバイスが見つかった場合は true、見つからない場合は false を返す。
    /// <para>
    /// Fetches the latest GPU statistics and sensor readings from IOKit and updates properties.
    /// Returns true if the matching device is found, false otherwise.
    /// </para>
    /// </summary>
    public bool Update()
    {
        var iterPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref iterPtr);
        if (kr != KERN_SUCCESS || iterPtr == IntPtr.Zero)
        {
            return false;
        }

        using var iter = new IORef(iterPtr);
        uint raw;
        while ((raw = IOIteratorNext(iter)) != 0)
        {
            using var entry = new IOObj(raw);
            var ioClass = entry.GetString("IOClass");
            if (string.Equals(ioClass, Name, StringComparison.Ordinal))
            {
                UpdateFromEntry(entry);
                return true;
            }
        }

        return false;
    }

    //--------------------------------------------------------------------------------
    // Private helpers
    //--------------------------------------------------------------------------------

    private void UpdateFromEntry(IOObj entry)
    {
        int? temperature = null;
        int? fanSpeed = null;
        int? coreClock = null;
        int? memoryClock = null;
        bool? powerState = null;

        using var perfDict = entry.GetDictionary("PerformanceStatistics");
        if (perfDict.IsValid)
        {
            DeviceUtilization = perfDict.GetInt64("Device Utilization %");
            RendererUtilization = perfDict.GetInt64("Renderer Utilization %");
            TilerUtilization = perfDict.GetInt64("Tiler Utilization %");
            AllocSystemMemory = perfDict.GetInt64("Alloc system memory");
            InUseSystemMemory = perfDict.GetInt64("In use system memory");
            InUseSystemMemoryDriver = perfDict.GetInt64("In use system memory (driver)");
            TiledSceneBytes = perfDict.GetInt64("TiledSceneBytes");
            AllocatedPBSize = perfDict.GetInt64("Allocated PB Size");
            RecoveryCount = perfDict.GetInt64("recoveryCount");
            SplitSceneCount = perfDict.GetInt64("SplitSceneCount");

            var rawTemp = perfDict.GetInt64("Temperature(C)");
            var rawFan = perfDict.GetInt64("Fan Speed(%)");
            var rawCore = perfDict.GetInt64("Core Clock(MHz)");
            var rawMem = perfDict.GetInt64("Memory Clock(MHz)");

            temperature = rawTemp > 0 && rawTemp < 128 ? (int)rawTemp : null;
            fanSpeed = rawFan > 0 ? (int)rawFan : null;
            coreClock = rawCore > 0 ? (int)rawCore : null;
            memoryClock = rawMem > 0 ? (int)rawMem : null;
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

        using var agcInfo = entry.GetDictionary("AGCInfo");
        if (agcInfo.IsValid)
        {
            var poweredOff = agcInfo.GetInt64("poweredOffByAGC");
            powerState = poweredOff == 0;
        }


        Temperature = temperature;
        FanSpeed = fanSpeed;
        CoreClock = coreClock;
        MemoryClock = memoryClock;
        PowerState = powerState;
        UpdateAt = DateTime.Now;
    }
}
