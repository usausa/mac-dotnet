namespace MacDotNet.SystemInfo;

#pragma warning disable CA1024
public static class PlatformProvider
{
    //--------------------------------------------------------------------------------
    // System
    //--------------------------------------------------------------------------------

    public static HardwareInfo GetHardware() => HardwareInfo.Create();

    public static KernelInfo GetKernel() => KernelInfo.Create();

    public static Uptime GetUptime() => Uptime.Create();

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

    public static CpuStat GetCpuStat() => CpuStat.Create();

    public static LoadAverage GetLoadAverage() => LoadAverage.Create();

    //--------------------------------------------------------------------------------
    // Memory
    //--------------------------------------------------------------------------------

    public static MemoryStat GetMemoryStat() => MemoryStat.Create();

    public static SwapUsage GetSwapUsage() => SwapUsage.Create();

    //--------------------------------------------------------------------------------
    // Storage
    //--------------------------------------------------------------------------------

    public static DiskStat GetDiskStat(bool includeAll = false) => DiskStat.Create(includeAll);

    public static FileSystemStat GetFileSystemStat(bool includeAll = false) => FileSystemStat.Create(includeAll);

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    public static NetworkStat GetNetworkStat(bool includeAll = false) => NetworkStat.Create(includeAll);

    //--------------------------------------------------------------------------------
    // Process
    //--------------------------------------------------------------------------------

    public static ProcessSummary GetProcessSummary() => ProcessSummary.Create();

    public static IReadOnlyList<ProcessInfo> GetProcesses() => ProcessInfo.GetProcesses();

    public static ProcessInfo? GetProcess(int processId) => ProcessInfo.GetProcess(processId);

    //--------------------------------------------------------------------------------
    // File
    //--------------------------------------------------------------------------------

    public static FileHandleStat GetFileHandleStat() => FileHandleStat.Create();

    //--------------------------------------------------------------------------------
    // CPU
    //--------------------------------------------------------------------------------

    public static CpuFrequency GetCpuFrequency() => CpuFrequency.Create();

    //--------------------------------------------------------------------------------
    // GPU
    //--------------------------------------------------------------------------------

    // Each element must be disposed
    public static IReadOnlyList<GpuDevice> GetGpuDevices() => GpuDevice.GetDevices();

    //--------------------------------------------------------------------------------
    // Power
    //--------------------------------------------------------------------------------

    public static PowerStat GetPowerStat() => PowerStat.Create();

    public static PowerManagementStat GetPowerManagementStat() => PowerManagementStat.Create();

    public static BatteryDevice GetBatteryDevice() => BatteryDevice.Create();

    public static MainsDevice GetMainsDevice() => MainsDevice.Create();

    //--------------------------------------------------------------------------------
    // Sensor
    //--------------------------------------------------------------------------------

    public static SmcMonitor GetSmcMonitor() => SmcMonitor.Create();
}
