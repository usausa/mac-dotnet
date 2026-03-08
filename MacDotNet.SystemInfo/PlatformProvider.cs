namespace MacDotNet.SystemInfo;

#pragma warning disable CA1024
public static class PlatformProvider
{
    //--------------------------------------------------------------------------------
    // System
    //--------------------------------------------------------------------------------

    // TODO
    //public static HardwareInfo GetHardware() => HardwareInfo.Create();

    // TODO
    //public static KernelInfo GetKernel() => KernelInfo.Create();

    public static Uptime GetUptime() => new();

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

    public static LoadAverage GetLoadAverage() => new();

    //--------------------------------------------------------------------------------
    // Memory
    //--------------------------------------------------------------------------------

    public static MemoryStat GetMemoryStat() => new();

    public static SwapUsage GetSwapUsage() => new();

    //--------------------------------------------------------------------------------
    // Storage
    //--------------------------------------------------------------------------------

    public static DiskStat GetDiskStat() => new();

    // TODO ?
    //public static IReadOnlyList<FileSystemEntry> GetFileSystems() => FileSystemInfo.GetFileSystems();

    public static FileSystemUsage GetFileSystemUsage(string path) => new(path);

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    public static NetworkStat GetNetworkStat() => new();

    //--------------------------------------------------------------------------------
    // Process
    //--------------------------------------------------------------------------------

    public static ProcessSummary GetProcessSummary() => new();

    // TODO 1
    //public static IReadOnlyList<ProcessEntry> GetProcesses() => ProcessInfo.GetProcesses();

    //--------------------------------------------------------------------------------
    // CPU
    //--------------------------------------------------------------------------------

    // TODO CPU ?

    //--------------------------------------------------------------------------------
    // GPU
    //--------------------------------------------------------------------------------

    // TODO
    //public static IReadOnlyList<GpuEntry> GetGpus() => GpuInfo.GetGpus();

    //--------------------------------------------------------------------------------
    // Power
    //--------------------------------------------------------------------------------

    public static PowerStat GetPowerStat() => new();

    //--------------------------------------------------------------------------------
    // Sensor
    //--------------------------------------------------------------------------------

    //public static IReadOnlyList<SmcSensorReading> GetTemperatureSensors() => SmcInfo.GetTemperatureSensors();

    //public static IReadOnlyList<SmcSensorReading> GetPowerReadings() => SmcInfo.GetPowerReadings();

    //public static IReadOnlyList<SmcSensorReading> GetVoltageReadings() => SmcInfo.GetVoltageReadings();

    //public static IReadOnlyList<SmcFanEntry> GetFans() => SmcInfo.GetFanInfo();

    //public static double? GetTotalSystemPower() => SmcInfo.GetTotalSystemPower();
}
