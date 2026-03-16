namespace MacDotNet.SystemInfo;

#pragma warning disable CA1024
public static class PlatformProvider
{
    //--------------------------------------------------------------------------------
    // System
    //--------------------------------------------------------------------------------

    public static HardwareInfo GetHardware() => new();

    public static KernelInfo GetKernel() => new();

    public static Uptime GetUptime() => new();

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

    public static CpuStat GetCpuStat() => new();

    public static LoadAverage GetLoadAverage() => new();

    //--------------------------------------------------------------------------------
    // Memory
    //--------------------------------------------------------------------------------

    public static MemoryStat GetMemoryStat() => new();

    public static SwapUsage GetSwapUsage() => new();

    //--------------------------------------------------------------------------------
    // Storage
    //--------------------------------------------------------------------------------

    public static DiskStat GetDiskStat(bool includeAll = false) => new(includeAll);

    public static FileSystemStat GetFileSystemStat(bool includeAll = false) => new(includeAll);

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    public static NetworkStat GetNetworkStat(bool includeAll = false) => new(includeAll);

    //--------------------------------------------------------------------------------
    // Process
    //--------------------------------------------------------------------------------

    public static ProcessSummary GetProcessSummary() => new();

    public static IReadOnlyList<ProcessInfo> GetProcesses() => ProcessInfo.GetProcesses();

    public static ProcessInfo? GetProcess(int processId) => ProcessInfo.GetProcess(processId);

    //--------------------------------------------------------------------------------
    // CPU
    //--------------------------------------------------------------------------------

    public static CpuFrequency GetCpuFrequency() => new();

    //--------------------------------------------------------------------------------
    // GPU
    //--------------------------------------------------------------------------------

    public static IReadOnlyList<GpuDevice> GetGpuDevices() => GpuDevice.GetDevices();

    //--------------------------------------------------------------------------------
    // Power
    //--------------------------------------------------------------------------------

    public static PowerStat GetPowerStat() => new();

    //--------------------------------------------------------------------------------
    // Sensor
    //--------------------------------------------------------------------------------

    public static SmcMonitor GetSmcMonitor() => new();
}
