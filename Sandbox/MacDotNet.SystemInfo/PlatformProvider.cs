namespace MacDotNet.SystemInfo;

#pragma warning disable CA1024
public static class PlatformProvider
{
    //--------------------------------------------------------------------------------
    // System
    //--------------------------------------------------------------------------------

    public static KernelInfo GetKernel() => KernelInfo.Create();

    public static HardwareInfo GetHardware() => HardwareInfo.Create();

    public static IReadOnlyList<PerformanceLevelEntry> GetPerformanceLevels() => HardwareInfo.GetPerformanceLevels();

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

    public static CpuStat GetCpuStat() => CpuStat.Create();

    //--------------------------------------------------------------------------------
    // Memory
    //--------------------------------------------------------------------------------

    public static MemoryStat GetMemoryStat() => new();

    public static SwapUsage GetSwapStat() => new();

    //--------------------------------------------------------------------------------
    // Storage
    //--------------------------------------------------------------------------------

    public static IReadOnlyList<FileSystemEntry> GetFileSystems() => FileSystemInfo.GetFileSystems();

    public static IReadOnlyList<DiskVolume> GetDiskVolumes() => FileSystemInfo.GetDiskVolumes();

    public static FileSystemUsage GetFileSystemUsage(string mountPoint) => new(mountPoint);

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    /// <summary>
    /// ネットワークインターフェースの一覧を返す。
    /// デフォルト (includeAll = false) では macOS System Settings のネットワーク設定と同じサービスのみを返す。
    /// includeAll = true にするとすべてのインターフェースを返す。
    /// </summary>
    /// <summary>
    /// ネットワークインターフェースの設定情報の一覧を返す。macOS SC 固有情報 (サービス名・種別・有効状態) を提供する。
    /// デフォルト (includeAll = false) では macOS System Settings に表示される有効なサービスのみを返す。
    /// includeAll = true にするとすべてのインターフェースを返す。
    /// <see cref="System.Net.NetworkInformation.NetworkInterface"/> と Name (BSD 名) で突合して使用する。
    /// </summary>
    public static IReadOnlyList<NetworkInterfaceEntry> GetNetworkInterfaces(bool includeAll = false) => NetworkInfo.GetNetworkInterfaces(includeAll);

    /// <summary>
    /// 全ネットワークインターフェースのトラフィック統計を返す。
    /// <see cref="NetworkStats.Update()"/> を呼ぶたびに累積値とデルタ値が更新される。
    /// </summary>
    public static NetworkStats GetNetworkStats() => NetworkStats.Create();

    //--------------------------------------------------------------------------------
    // Process
    //--------------------------------------------------------------------------------

    public static ProcessInfo[] GetProcesses() => ProcessInfo.GetProcesses();

    public static ProcessInfo? GetProcess(int processId) => ProcessInfo.GetProcess(processId);

    public static ProcessSummary GetProcessSummary() => new();

    //--------------------------------------------------------------------------------
    // GPU
    //--------------------------------------------------------------------------------

    public static GpuInfo[] GetGpuInfos() => HardwareInfo.GetGpus();

    public static GpuDevice[] GetGpuDevices() => GpuDevice.GetDevices();

    //--------------------------------------------------------------------------------
    // Power
    //--------------------------------------------------------------------------------

    public static Battery GetBattery() => new();

    public static BatteryDetail GetBatteryDetail() => BatteryDetail.Create();

    public static BatteryGeneric GetBatteryGeneric() => BatteryGeneric.Create();

    public static AppleSiliconEnergyCounter GetAppleSiliconEnergyCounter() => AppleSiliconEnergyCounter.Create();

    //--------------------------------------------------------------------------------
    // Sensor
    //--------------------------------------------------------------------------------

    public static IReadOnlyList<SmcSensorReading> GetTemperatureSensors() => SmcInfo.GetTemperatureSensors();

    public static IReadOnlyList<SmcSensorReading> GetPowerReadings() => SmcInfo.GetPowerReadings();

    public static IReadOnlyList<SmcSensorReading> GetVoltageReadings() => SmcInfo.GetVoltageReadings();

    public static IReadOnlyList<SmcFanEntry> GetFans() => SmcInfo.GetFanInfo();

    public static double? GetTotalSystemPower() => SmcInfo.GetTotalSystemPower();
}

