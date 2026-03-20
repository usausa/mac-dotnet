namespace Example.SystemInfo.ConsoleApp;

#pragma warning disable SA1401

using MacDotNet.SystemInfo;

public sealed class DiskDeviceEntry
{
    internal readonly DiskDeviceStat Stat;

    internal bool Live;

    internal ulong PreviousBytesRead;

    internal ulong PreviousBytesWrite;

    // Delegation properties
    public string Name => Stat.BsdName;
    public DiskBusType BusType => Stat.BusType;
    public ulong DiskSize => Stat.DiskSize;

    // Calculated rates (set by SystemMonitor)
    public double ReadBytesPerSec { get; internal set; }
    public double WriteBytesPerSec { get; internal set; }

    internal DiskDeviceEntry(DiskDeviceStat diskDeviceStat)
    {
        Stat = diskDeviceStat;
        PreviousBytesRead = diskDeviceStat.BytesRead;
        PreviousBytesWrite = diskDeviceStat.BytesWrite;
        Live = true;
    }
}

public sealed class FileSystemMonitorEntry
{
    internal readonly FileSystemEntry Entry;

    internal bool Live;

    // Delegation properties
    public string MountPoint => Entry.MountPoint;
    public string FileSystem => Entry.FileSystem;
    public ulong TotalSize => Entry.TotalSize;
    public ulong FreeSize => Entry.FreeSize;
    public ulong AvailableSize => Entry.AvailableSize;

    internal FileSystemMonitorEntry(FileSystemEntry fileSystemEntry)
    {
        Entry = fileSystemEntry;
        Live = true;
    }
}

public sealed class NetworkIfEntry
{
    internal readonly NetworkStatEntry Stat;

    internal bool Live;

    internal uint PreviousRxBytes;

    internal uint PreviousTxBytes;

    // Delegation properties
    public string Name => Stat.Name;
    public string? DisplayName => Stat.DisplayName;
    public uint RxBytes => Stat.RxBytes;
    public uint TxBytes => Stat.TxBytes;

    // Calculated rates (set by SystemMonitor)
    public double RxBytesPerSec { get; internal set; }
    public double TxBytesPerSec { get; internal set; }

    internal NetworkIfEntry(NetworkStatEntry networkStatEntry)
    {
        Stat = networkStatEntry;
        PreviousRxBytes = networkStatEntry.RxBytes;
        PreviousTxBytes = networkStatEntry.TxBytes;
        Live = true;
    }
}

public sealed class GpuEntry
{
    internal readonly GpuDevice Device;

    // Delegation properties
    public string Name => Device.Name;
    public long DeviceUtilization => Device.DeviceUtilization;
    public long RendererUtilization => Device.RendererUtilization;
    public long TilerUtilization => Device.TilerUtilization;
    public int Temperature => Device.Temperature;

    internal GpuEntry(GpuDevice gpuDevice) => Device = gpuDevice;
}

public sealed class FanSensorEntry
{
    private readonly FanSensor sensor;

    public int Index => sensor.Index;
    public double ActualRpm => sensor.ActualRpm;
    public double MinRpm => sensor.MinRpm;
    public double MaxRpm => sensor.MaxRpm;

    internal FanSensorEntry(FanSensor fanSensor) => sensor = fanSensor;
}

internal sealed class SystemMonitor
{
    //--------------------------------------------------------------------------------
    // System info providers
    //--------------------------------------------------------------------------------

    private readonly Uptime uptime;
    private readonly CpuStat cpuStat;
    private readonly CpuFrequency cpuFrequency;
    private readonly LoadAverage loadAverage;
    private readonly MemoryStat memoryStat;
    private readonly SwapUsage swapUsage;
    private readonly DiskStat diskStat;
    private readonly NetworkStat networkStat;
    private readonly ProcessSummary processSummary;
    private readonly PowerStat powerStat;
    private readonly SmcMonitor smcMonitor;
    private readonly FileSystemStat fileSystemStat;

    //--------------------------------------------------------------------------------
    // Field
    //--------------------------------------------------------------------------------

    private readonly List<DiskDeviceEntry> diskEntries = [];
    private readonly List<NetworkIfEntry> networkEntries = [];
    private readonly List<FileSystemMonitorEntry> fileSystemEntries = [];
    private readonly List<GpuEntry> gpuEntries = [];
    private readonly List<FanSensorEntry> fanEntries = [];

    // CPU counter
    private record struct CpuCoreCounters(uint User, uint System, uint Idle, uint Nice);

    private readonly CpuCoreCounters[] prevAllCoreCounters;
    private readonly CpuCoreCounters[] prevEfficiencyCoreCounters;
    private readonly CpuCoreCounters[] prevPerformanceCoreCounters;

    // Power counters
    private double prevPowerCpuJ;
    private double prevPowerGpuJ;
    private double prevPowerAneJ;
    private double prevPowerRamJ;
    private double prevPowerPciJ;

    // Pre-computed CPU usage
    private double cpuUsageTotal;
    private double cpuUsageEfficiency;
    private double cpuUsagePerformance;
    private double cpuUserPercent;
    private double cpuSystemPercent;
    private double cpuIdlePercent;

    // Pre-computed CPU frequency
    private double cpuFrequencyAllHz;
    private double cpuFrequencyEfficiencyHz;
    private double cpuFrequencyPerformanceHz;

    // Pre-computed memory / swap
    private double memoryUsagePercent;
    private double memoryActivePercent;
    private double memoryWiredPercent;
    private double memoryCompressorPercent;
    private double swapUsagePercent;

    // Pre-computed power rates
    private double powerCpuW;
    private double powerGpuW;
    private double powerAneW;
    private double powerRamW;
    private double powerPciW;

    // Individual sensor references (set during initialization)
#pragma warning disable SA1214
    // ReSharper disable CommentTypo
    private readonly TemperatureSensor? sensorCpuDieAvg;      // TCMb
    private readonly TemperatureSensor? sensorNand;           // TH0x
    private readonly TemperatureSensor? sensorSsd;            // TPSD
    private readonly TemperatureSensor? sensorMainboard;      // Tm0P
    private readonly VoltageSensor? sensorDcInVoltage;        // VD0R
    private readonly CurrentSensor? sensorDcInCurrent;        // ID0R
    private readonly PowerSensor? sensorDcInPower;            // Pb0f
    private readonly PowerSensor? sensorTotalSystemPower;     // PDTR
    // ReSharper restore CommentTypo
#pragma warning restore SA1214

    private DateTime lastUpdateTime;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    // CPU Usage

    public double CpuUsageTotal => cpuUsageTotal;
    public double CpuUsageEfficiency => cpuUsageEfficiency;
    public double CpuUsagePerformance => cpuUsagePerformance;
    public double CpuUserPercent => cpuUserPercent;
    public double CpuSystemPercent => cpuSystemPercent;
    public double CpuIdlePercent => cpuIdlePercent;

    // CPU Frequency

    public double CpuFrequencyAllHz => cpuFrequencyAllHz;
    public double CpuFrequencyEfficiencyHz => cpuFrequencyEfficiencyHz;
    public double CpuFrequencyPerformanceHz => cpuFrequencyPerformanceHz;

    // Uptime

    public TimeSpan Uptime => uptime.Elapsed;

    // Load

    public double LoadAverage1 => loadAverage.Average1;
    public double LoadAverage5 => loadAverage.Average5;
    public double LoadAverage15 => loadAverage.Average15;

    // Process

    public int ProcessCount => processSummary.ProcessCount;
    public int ThreadCount => processSummary.ThreadCount;
    public int OpenFileCount => processSummary.OpenFileCount;

    // Memory

    public double MemoryUsagePercent => memoryUsagePercent;
    public double MemoryActivePercent => memoryActivePercent;
    public double MemoryWiredPercent => memoryWiredPercent;
    public double MemoryCompressorPercent => memoryCompressorPercent;

    // Swap

    public double SwapUsagePercent => swapUsagePercent;

    // Disk

    public IReadOnlyList<DiskDeviceEntry> DiskDevices => diskEntries;

    public IReadOnlyList<FileSystemMonitorEntry> FileSystems => fileSystemEntries;

    // Network

    public IReadOnlyList<NetworkIfEntry> NetworkInterfaces => networkEntries;

    // GPU

    public IReadOnlyList<GpuEntry> GpuDevices => gpuEntries;

    // Temperature

    public double? CpuTemperature => sensorCpuDieAvg?.Value;
    public double? NandTemperature => sensorNand?.Value;
    public double? SsdTemperature => sensorSsd?.Value;
    public double? MainboardTemperature => sensorMainboard?.Value;

    // Voltage

    public double? DcInVoltage => sensorDcInVoltage?.Value;

    // Current

    public double? DcInCurrent => sensorDcInCurrent?.Value;

    // Power

    public double? DcInPower => sensorDcInPower?.Value;

    public double? TotalSystemPower => sensorTotalSystemPower?.Value;

    // Power

    public IReadOnlyList<FanSensorEntry> Fans => fanEntries;

    // Power Consumption

    public double PowerCpuW => powerCpuW;
    public double PowerGpuW => powerGpuW;
    public double PowerAneW => powerAneW;
    public double PowerRamW => powerRamW;
    public double PowerPciW => powerPciW;
    public double PowerTotalW => powerCpuW + powerGpuW + powerAneW + powerRamW + powerPciW;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public SystemMonitor()
    {
        lastUpdateTime = DateTime.UtcNow;

        uptime = PlatformProvider.GetUptime();
        cpuStat = PlatformProvider.GetCpuStat();
        cpuFrequency = PlatformProvider.GetCpuFrequency();
        loadAverage = PlatformProvider.GetLoadAverage();
        memoryStat = PlatformProvider.GetMemoryStat();
        swapUsage = PlatformProvider.GetSwapUsage();
        diskStat = PlatformProvider.GetDiskStat();
        networkStat = PlatformProvider.GetNetworkStat();
        processSummary = PlatformProvider.GetProcessSummary();
        powerStat = PlatformProvider.GetPowerStat();
        smcMonitor = PlatformProvider.GetSmcMonitor();
        fileSystemStat = PlatformProvider.GetFileSystemStat();

        // CPU
        prevAllCoreCounters = cpuStat.CpuCores.Select(c => new CpuCoreCounters(c.User, c.System, c.Idle, c.Nice)).ToArray();
        prevEfficiencyCoreCounters = cpuStat.EfficiencyCores.Select(c => new CpuCoreCounters(c.User, c.System, c.Idle, c.Nice)).ToArray();
        prevPerformanceCoreCounters = cpuStat.PerformanceCores.Select(c => new CpuCoreCounters(c.User, c.System, c.Idle, c.Nice)).ToArray();
        // GPU
        gpuEntries.AddRange(PlatformProvider.GetGpuDevices().Select(d => new GpuEntry(d)));
        // Sensor
        // ReSharper disable StringLiteralTypo
        var temps = smcMonitor.Temperatures;
        sensorCpuDieAvg = temps.FirstOrDefault(t => t.Key == "TCMb");
        sensorNand = temps.FirstOrDefault(t => t.Key == "TH0x");
        sensorSsd = temps.FirstOrDefault(t => t.Key == "TPSD");
        sensorMainboard = temps.FirstOrDefault(t => t.Key == "Tm0P");
        sensorDcInVoltage = smcMonitor.Voltages.FirstOrDefault(v => v.Key == "VD0R");
        sensorDcInCurrent = smcMonitor.Currents.FirstOrDefault(c => c.Key == "ID0R");
        sensorDcInPower = smcMonitor.Powers.FirstOrDefault(p => p.Key == "Pb0f");
        sensorTotalSystemPower = smcMonitor.Powers.FirstOrDefault(p => p.Key == "PDTR");
        // ReSharper restore StringLiteralTypo
        fanEntries.AddRange(smcMonitor.Fans.Select(f => new FanSensorEntry(f)));

        CalculateCpuFrequency();
        CalculateMemoryAndSwap();
        CalculateDiskEntries(0);
        CalculateFileSystemEntries();
        CalculateNetworkEntries(0);

        SavePowerCounters();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public void Update()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - lastUpdateTime).TotalSeconds;
        lastUpdateTime = now;

        uptime.Update();
        cpuStat.Update();
        cpuFrequency.Update();
        loadAverage.Update();
        memoryStat.Update();
        swapUsage.Update();
        diskStat.Update();
        networkStat.Update();
        processSummary.Update();
        fileSystemStat.Update();

        for (var i = 0; i < gpuEntries.Count; i++)
        {
            gpuEntries[i].Device.Update();
        }

        powerStat.Update();
        smcMonitor.Update();

        CalculateCpuUsage();
        CalculateCpuFrequency();
        CalculateMemoryAndSwap();
        CalculateDiskEntries(elapsed);
        CalculateFileSystemEntries();
        CalculateNetworkEntries(elapsed);
        CalculatePowerRates(elapsed);
    }

    //--------------------------------------------------------------------------------
    // CPU
    //--------------------------------------------------------------------------------

    private void CalculateCpuUsage()
    {
        CalcAllCoresUsage(cpuStat.CpuCores, prevAllCoreCounters, out cpuUsageTotal, out cpuUserPercent, out cpuSystemPercent, out cpuIdlePercent);
        CalcGroupUsage(cpuStat.EfficiencyCores, prevEfficiencyCoreCounters, out cpuUsageEfficiency);
        CalcGroupUsage(cpuStat.PerformanceCores, prevPerformanceCoreCounters, out cpuUsagePerformance);

        SavePreviousCoreCounters(cpuStat.CpuCores, prevAllCoreCounters);
        SavePreviousCoreCounters(cpuStat.EfficiencyCores, prevEfficiencyCoreCounters);
        SavePreviousCoreCounters(cpuStat.PerformanceCores, prevPerformanceCoreCounters);
    }

    private static void CalcAllCoresUsage(
        IReadOnlyList<CpuCoreStat> cores,
        CpuCoreCounters[] prev,
        out double usage,
        out double userPct,
        out double sysPct,
        out double idlePct)
    {
        var dUser = 0ul;
        var dSystem = 0ul;
        var dIdle = 0ul;
        var dNice = 0ul;
        for (var i = 0; i < cores.Count; i++)
        {
            dUser += cores[i].User - prev[i].User;
            dSystem += cores[i].System - prev[i].System;
            dIdle += cores[i].Idle - prev[i].Idle;
            dNice += cores[i].Nice - prev[i].Nice;
        }

        var total = dUser + dSystem + dIdle + dNice;
        if (total == 0)
        {
            usage = userPct = sysPct = idlePct = 0;
            return;
        }

        usage = (double)(dUser + dSystem) / total * 100.0;
        userPct = (double)dUser / total * 100.0;
        sysPct = (double)dSystem / total * 100.0;
        idlePct = (double)dIdle / total * 100.0;
    }

    private static void CalcGroupUsage(IReadOnlyList<CpuCoreStat> cores, CpuCoreCounters[] prev, out double usage)
    {
        var dUser = 0ul;
        var dSystem = 0ul;
        var dIdle = 0ul;
        var dNice = 0ul;
        for (var i = 0; i < cores.Count; i++)
        {
            dUser += cores[i].User - prev[i].User;
            dSystem += cores[i].System - prev[i].System;
            dIdle += cores[i].Idle - prev[i].Idle;
            dNice += cores[i].Nice - prev[i].Nice;
        }

        var total = dUser + dSystem + dIdle + dNice;
        usage = total == 0 ? 0 : (double)(dUser + dSystem) / total * 100.0;
    }

    private static void SavePreviousCoreCounters(IReadOnlyList<CpuCoreStat> cores, CpuCoreCounters[] prev)
    {
        for (var i = 0; i < cores.Count; i++)
        {
            prev[i] = new CpuCoreCounters(cores[i].User, cores[i].System, cores[i].Idle, cores[i].Nice);
        }
    }

    //--------------------------------------------------------------------------------
    // CPU Frequency
    //--------------------------------------------------------------------------------

    private void CalculateCpuFrequency()
    {
        cpuFrequencyAllHz = CalcAvgFrequencyHz(cpuFrequency.Cores);
        cpuFrequencyEfficiencyHz = CalcAvgFrequencyHz(cpuFrequency.EfficiencyCores);
        cpuFrequencyPerformanceHz = CalcAvgFrequencyHz(cpuFrequency.PerformanceCores);
    }

    private static double CalcAvgFrequencyHz(IReadOnlyList<CpuCoreFrequency> cores)
    {
        if (cores.Count == 0)
        {
            return 0;
        }

        var sum = 0.0;
        for (var i = 0; i < cores.Count; i++)
        {
            sum += cores[i].Frequency;
        }

        return sum / cores.Count * 1_000_000.0;
    }

    //--------------------------------------------------------------------------------
    // Memory / Swap
    //--------------------------------------------------------------------------------

    private void CalculateMemoryAndSwap()
    {
        var total = memoryStat.PhysicalMemory;
        if (total > 0)
        {
            memoryUsagePercent = (double)memoryStat.UsedBytes / total * 100.0;
            memoryActivePercent = (double)memoryStat.ActiveBytes / total * 100.0;
            memoryWiredPercent = (double)memoryStat.WiredBytes / total * 100.0;
            memoryCompressorPercent = (double)memoryStat.CompressorBytes / total * 100.0;
        }
        else
        {
            memoryUsagePercent = 0;
            memoryActivePercent = 0;
            memoryWiredPercent = 0;
            memoryCompressorPercent = 0;
        }

        swapUsagePercent = swapUsage.TotalBytes > 0
            ? (double)swapUsage.UsedBytes / swapUsage.TotalBytes * 100.0
            : 0;
    }

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    private void CalculateDiskEntries(double elapsed)
    {
        var devices = diskStat.Devices;

        for (var i = 0; i < diskEntries.Count; i++)
        {
            diskEntries[i].Live = false;
        }

        var added = false;
        for (var i = 0; i < devices.Count; i++)
        {
            var entry = default(DiskDeviceEntry);
            for (var j = 0; j < diskEntries.Count; j++)
            {
                if (devices[i] == diskEntries[j].Stat)
                {
                    entry = diskEntries[j];
                    break;
                }
            }

            if (entry is null)
            {
                entry = new DiskDeviceEntry(devices[i]);
                diskEntries.Add(entry);
                added = true;
            }

            UpdateDiskEntry(entry, elapsed);

            entry.Live = true;
        }

        for (var i = diskEntries.Count - 1; i >= 0; i--)
        {
            if (!diskEntries[i].Live)
            {
                diskEntries.RemoveAt(i);
            }
        }

        if (added)
        {
            diskEntries.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.Name, y.Name));
        }
    }

    private static void UpdateDiskEntry(DiskDeviceEntry entry, double elapsed)
    {
        if (elapsed > 0)
        {
            var readDelta = entry.Stat.BytesRead >= entry.PreviousBytesRead ? entry.Stat.BytesRead - entry.PreviousBytesRead : 0;
            var writeDelta = entry.Stat.BytesWrite >= entry.PreviousBytesWrite ? entry.Stat.BytesWrite - entry.PreviousBytesWrite : 0;
            entry.ReadBytesPerSec = readDelta / elapsed;
            entry.WriteBytesPerSec = writeDelta / elapsed;
        }

        entry.PreviousBytesRead = entry.Stat.BytesRead;
        entry.PreviousBytesWrite = entry.Stat.BytesWrite;
    }

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    private void CalculateNetworkEntries(double elapsed)
    {
        var ifaces = networkStat.Interfaces;

        for (var i = 0; i < networkEntries.Count; i++)
        {
            networkEntries[i].Live = false;
        }

        var added = false;
        for (var i = 0; i < ifaces.Count; i++)
        {
            if (!ifaces[i].IsEnabled)
            {
                continue;
            }

            var entry = default(NetworkIfEntry);
            for (var j = 0; j < networkEntries.Count; j++)
            {
                if (ifaces[i] == networkEntries[j].Stat)
                {
                    entry = networkEntries[j];
                    break;
                }
            }

            if (entry is null)
            {
                entry = new NetworkIfEntry(ifaces[i]);
                networkEntries.Add(entry);
                added = true;
            }

            UpdateNetworkEntry(entry, elapsed);

            entry.Live = true;
        }

        for (var i = networkEntries.Count - 1; i >= 0; i--)
        {
            if (!networkEntries[i].Live)
            {
                networkEntries.RemoveAt(i);
            }
        }

        if (added)
        {
            networkEntries.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.Name, y.Name));
        }
    }

    private static void UpdateNetworkEntry(NetworkIfEntry entry, double elapsed)
    {
        if (elapsed > 0)
        {
            var rxDelta = entry.Stat.RxBytes >= entry.PreviousRxBytes ? entry.Stat.RxBytes - entry.PreviousRxBytes : 0;
            var txDelta = entry.Stat.TxBytes >= entry.PreviousTxBytes ? entry.Stat.TxBytes - entry.PreviousTxBytes : 0;
            entry.RxBytesPerSec = rxDelta / elapsed;
            entry.TxBytesPerSec = txDelta / elapsed;
        }

        entry.PreviousRxBytes = entry.Stat.RxBytes;
        entry.PreviousTxBytes = entry.Stat.TxBytes;
    }

    //--------------------------------------------------------------------------------
    // FileSystem
    //--------------------------------------------------------------------------------

    private void CalculateFileSystemEntries()
    {
        var entries = fileSystemStat.Entries;

        for (var i = 0; i < fileSystemEntries.Count; i++)
        {
            fileSystemEntries[i].Live = false;
        }

        var added = false;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = default(FileSystemMonitorEntry);
            for (var j = 0; j < fileSystemEntries.Count; j++)
            {
                if (entries[i] == fileSystemEntries[j].Entry)
                {
                    entry = fileSystemEntries[j];
                    break;
                }
            }

            if (entry is null)
            {
                entry = new FileSystemMonitorEntry(entries[i]);
                fileSystemEntries.Add(entry);
                added = true;
            }

            entry.Live = true;
        }

        for (var i = fileSystemEntries.Count - 1; i >= 0; i--)
        {
            if (!fileSystemEntries[i].Live)
            {
                fileSystemEntries.RemoveAt(i);
            }
        }

        if (added)
        {
            fileSystemEntries.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.MountPoint, y.MountPoint));
        }
    }

    //--------------------------------------------------------------------------------
    // Power
    //--------------------------------------------------------------------------------

    private void SavePowerCounters()
    {
        prevPowerCpuJ = powerStat.Cpu;
        prevPowerGpuJ = powerStat.Gpu;
        prevPowerAneJ = powerStat.Ane;
        prevPowerRamJ = powerStat.Ram;
        prevPowerPciJ = powerStat.Pci;
    }

    private void CalculatePowerRates(double elapsed)
    {
        if ((elapsed > 0) && powerStat.Supported)
        {
            powerCpuW = (powerStat.Cpu - prevPowerCpuJ) / elapsed;
            powerGpuW = (powerStat.Gpu - prevPowerGpuJ) / elapsed;
            powerAneW = (powerStat.Ane - prevPowerAneJ) / elapsed;
            powerRamW = (powerStat.Ram - prevPowerRamJ) / elapsed;
            powerPciW = (powerStat.Pci - prevPowerPciJ) / elapsed;
        }

        SavePowerCounters();
    }
}
