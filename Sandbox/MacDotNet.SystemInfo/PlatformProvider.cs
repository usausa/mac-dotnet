namespace MacDotNet.SystemInfo;

#pragma warning disable CA1024
public static class PlatformProvider
{
    //--------------------------------------------------------------------------------
    // System
    //--------------------------------------------------------------------------------

    public static HardwareInfo GetHardware() => HardwareInfo.Create();

    public static KernelInfo GetKernel() => KernelInfo.Create();

    public static IReadOnlyList<PerformanceLevelEntry> GetPerformanceLevels() => HardwareInfo.GetPerformanceLevels();

    //--------------------------------------------------------------------------------
    // CPU
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

    public static IReadOnlyList<FileSystemInfo> GetFileSystems(bool includeAll = false) => FileSystemInfo.GetFileSystems(includeAll);

    public static FileSystemUsage GetFileSystemUsage(string path) => new(path);

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    public static IReadOnlyList<NetworkInterfaceEntry> GetNetworkInterfaces(bool includeAll = false) => NetworkInfo.GetNetworkInterfaces(includeAll);

    public static NetworkStat GetNetworkStats(bool includeAll = false) => new(includeAll);

    //--------------------------------------------------------------------------------
    // Process
    //--------------------------------------------------------------------------------

    public static ProcessSummary GetProcessSummary() => new();

    public static ProcessInfo[] GetProcesses() => ProcessInfo.GetProcesses();

    //--------------------------------------------------------------------------------
    // GPU
    //--------------------------------------------------------------------------------

    public static GpuInfo[] GetGpuInfos() => HardwareInfo.GetGpus();

    public static GpuDevice[] GetGpuDevices() => GpuDevice.GetDevices();

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    public static DiskStat GetDiskStats(bool includeAll = false) => new(includeAll);

    //--------------------------------------------------------------------------------
    // Power / Battery
    //--------------------------------------------------------------------------------

    public static Battery GetBattery() => new();

    public static BatteryDetail GetBatteryDetail() => BatteryDetail.Create();

    public static BatteryGeneric GetBatteryGeneric() => BatteryGeneric.Create();

    public static AppleSiliconEnergyCounter GetAppleSiliconEnergyCounter() => AppleSiliconEnergyCounter.Create();

    //--------------------------------------------------------------------------------
    // Sensor
    //--------------------------------------------------------------------------------

    public static HardwareMonitor? GetHardwareMonitor() => HardwareMonitor.Create();
}
