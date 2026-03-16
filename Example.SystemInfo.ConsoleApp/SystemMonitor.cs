namespace Example.SystemInfo.ConsoleApp;

using MacDotNet.SystemInfo;

internal sealed class SystemMonitor
{
    // --- System info objects ---
    private readonly Uptime uptime;
    private readonly CpuStat cpuStat;
    private readonly CpuFrequency cpuFrequency;
    private readonly LoadAverage loadAverage;
    private readonly MemoryStat memoryStat;
    private readonly SwapUsage swapUsage;
    private readonly DiskStat diskStat;
    private readonly NetworkStat networkStat;
    private readonly ProcessSummary processSummary;
    private readonly IReadOnlyList<GpuDevice> gpuDevices;
    private readonly PowerStat powerStat;
    private readonly SmcMonitor smcMonitor;
    private readonly FileSystemStat fileSystemStat;

    private DateTime lastUpdateTime;

    // --- Previous counter values for rate calculation ---
    private record struct CpuCoreCounters(uint User, uint System, uint Idle, uint Nice);

    private CpuCoreCounters[] prevCpuCounters = [];

    private record struct DiskDeviceCounters(ulong BytesRead, ulong BytesWrite);

    private readonly Dictionary<string, DiskDeviceCounters> prevDiskCounters = new();

    private record struct NetworkIfCounters(uint RxBytes, uint TxBytes);

    private readonly Dictionary<string, NetworkIfCounters> prevNetCounters = new();

    // --- Calculated fields ---
    private readonly Dictionary<string, double> diskReadBytesPerSec = new();

    private readonly Dictionary<string, double> diskWriteBytesPerSec = new();

    private readonly Dictionary<string, double> netRxBytesPerSec = new();

    private readonly Dictionary<string, double> netTxBytesPerSec = new();

    private double prevPowerCpuJ;
    private double prevPowerGpuJ;
    private double prevPowerAneJ;
    private double prevPowerRamJ;
    private double prevPowerPciJ;

    private double cpuUsageTotal;
    private double cpuUsageEfficiency;
    private double cpuUsagePerformance;
    private double cpuUserPercent;
    private double cpuSystemPercent;
    private double cpuIdlePercent;

    private double powerCpuW;
    private double powerGpuW;
    private double powerAneW;
    private double powerRamW;
    private double powerPciW;

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

    // Uptime

    public TimeSpan Uptime => uptime.Elapsed;

    // Load Average

    public double LoadAverage1 => loadAverage.Average1;
    public double LoadAverage5 => loadAverage.Average5;
    public double LoadAverage15 => loadAverage.Average15;

    // CPU Frequency (Hz)

    public double CpuFrequencyAllHz => cpuFrequency.Cores.Count > 0
        ? cpuFrequency.Cores.Average(c => c.Frequency) * 1_000_000.0
        : 0;

    public double CpuFrequencyEfficiencyHz => cpuFrequency.EfficiencyCores.Count > 0
        ? cpuFrequency.EfficiencyCores.Average(c => c.Frequency) * 1_000_000.0
        : 0;

    public double CpuFrequencyPerformanceHz => cpuFrequency.PerformanceCores.Count > 0
        ? cpuFrequency.PerformanceCores.Average(c => c.Frequency) * 1_000_000.0
        : 0;

    // Process

    public int ProcessCount => processSummary.ProcessCount;

    public int ThreadCount => processSummary.ThreadCount;

    // GPU

    public IReadOnlyList<GpuDevice> GpuDevices => gpuDevices;

    // Memory
    public double MemoryUsagePercent => memoryStat.PhysicalMemory > 0
        ? (double)memoryStat.UsedBytes / memoryStat.PhysicalMemory * 100.0
        : 0;

    public double MemoryAppPercent => memoryStat.PhysicalMemory > 0
        ? (double)memoryStat.AppMemoryBytes / memoryStat.PhysicalMemory * 100.0
        : 0;

    public double MemoryWiredPercent => memoryStat.PhysicalMemory > 0
        ? (double)memoryStat.WiredBytes / memoryStat.PhysicalMemory * 100.0
        : 0;

    public double MemoryCompressorPercent => memoryStat.PhysicalMemory > 0
        ? (double)memoryStat.CompressorBytes / memoryStat.PhysicalMemory * 100.0
        : 0;

    // Swap

    public double SwapUsagePercent => swapUsage.TotalBytes > 0
        ? (double)swapUsage.UsedBytes / swapUsage.TotalBytes * 100.0
        : 0;

    // Disk

    public IReadOnlyList<DiskDeviceStat> DiskDevices => diskStat.Devices;

    public IReadOnlyList<FileSystemEntry> FileSystems => fileSystemStat.Entries;

    public double GetDiskReadBytesPerSec(string deviceName) => diskReadBytesPerSec.GetValueOrDefault(deviceName);

    public double GetDiskWriteBytesPerSec(string deviceName) => diskWriteBytesPerSec.GetValueOrDefault(deviceName);

    // Network

    public IReadOnlyList<NetworkStatEntry> NetworkInterfaces => networkStat.Interfaces;

    public double GetNetworkRxBytesPerSec(string name) => netRxBytesPerSec.GetValueOrDefault(name);

    public double GetNetworkTxBytesPerSec(string name) => netTxBytesPerSec.GetValueOrDefault(name);

    public ulong GetNetworkTotalRxBytes(string name) =>
        networkStat.Interfaces.FirstOrDefault(i => i.Name == name)?.RxBytes ?? 0;

    public ulong GetNetworkTotalTxBytes(string name) =>
        networkStat.Interfaces.FirstOrDefault(i => i.Name == name)?.TxBytes ?? 0;

    // Sensors

    public IReadOnlyList<TemperatureSensor> Temperatures => smcMonitor.Temperatures;

    public IReadOnlyList<VoltageSensor> Voltages => smcMonitor.Voltages;

    public IReadOnlyList<CurrentSensor> Currents => smcMonitor.Currents;

    public IReadOnlyList<PowerSensor> SensorPowers => smcMonitor.Powers;

    public IReadOnlyList<FanSensor> Fans => smcMonitor.Fans;

    // Power Consumption

    public double PowerCpuW => powerCpuW;

    public double PowerGpuW => powerGpuW;

    public double PowerAneW => powerAneW;

    public double PowerRamW => powerRamW;

    public double PowerPciW => powerPciW;

    public double PowerTotalW => powerCpuW + powerGpuW + powerAneW + powerRamW + powerPciW;

    // Total cumulative energy

    public double PowerTotalWh => powerStat.Total;

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
        gpuDevices = PlatformProvider.GetGpuDevices();
        powerStat = PlatformProvider.GetPowerStat();
        smcMonitor = PlatformProvider.GetSmcMonitor();
        fileSystemStat = PlatformProvider.GetFileSystemStat();

        SaveCpuCounters();
        SaveDiskCounters();
        SaveNetworkCounters();
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
        foreach (var gpu in gpuDevices)
        {
            gpu.Update();
        }
        powerStat.Update();
        smcMonitor.Update();

        // Calculate
        CalculateCpuUsage();
        CalculateDiskRates(elapsed);
        CalculateNetworkRates(elapsed);
        CalculatePowerRates(elapsed);
    }

    //--------------------------------------------------------------------------------
    // CPU
    //--------------------------------------------------------------------------------

    private void SaveCpuCounters()
    {
        var cores = cpuStat.CpuCores;
        prevCpuCounters = new CpuCoreCounters[cores.Count];
        for (var i = 0; i < cores.Count; i++)
        {
            prevCpuCounters[i] = new CpuCoreCounters(cores[i].User, cores[i].System, cores[i].Idle, cores[i].Nice);
        }
    }

    private void CalculateCpuUsage()
    {
        var cores = cpuStat.CpuCores;
        if (prevCpuCounters.Length != cores.Count)
        {
            SaveCpuCounters();
            return;
        }

        var allIndices = Enumerable.Range(0, cores.Count).ToArray();
        CalcCoreGroupUsage(cores, allIndices, out cpuUsageTotal, out cpuUserPercent, out cpuSystemPercent, out cpuIdlePercent);

        var eIndices = cores.Select((c, i) => (c, i))
            .Where(x => x.c.CoreType == CpuCoreType.Efficiency)
            .Select(x => x.i).ToArray();
        CalcCoreGroupUsage(cores, eIndices, out cpuUsageEfficiency, out _, out _, out _);

        var pIndices = cores.Select((c, i) => (c, i))
            .Where(x => x.c.CoreType == CpuCoreType.Performance)
            .Select(x => x.i).ToArray();
        CalcCoreGroupUsage(cores, pIndices, out cpuUsagePerformance, out _, out _, out _);

        SaveCpuCounters();
    }

    private void CalcCoreGroupUsage(
        IReadOnlyList<CpuCoreStat> cores,
        IEnumerable<int> indices,
        out double usage,
        out double userPct,
        out double sysPct,
        out double idlePct)
    {
        var dUser = 0ul;
        var dSystem = 0ul;
        var dIdle = 0ul;
        var dNice = 0ul;
        foreach (var i in indices)
        {
            dUser += cores[i].User - prevCpuCounters[i].User;
            dSystem += cores[i].System - prevCpuCounters[i].System;
            dIdle += cores[i].Idle - prevCpuCounters[i].Idle;
            dNice += cores[i].Nice - prevCpuCounters[i].Nice;
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

    //--------------------------------------------------------------------------------
    // Disk
    //--------------------------------------------------------------------------------

    private void SaveDiskCounters()
    {
        foreach (var device in diskStat.Devices)
        {
            prevDiskCounters[device.Name] = new DiskDeviceCounters(device.BytesRead, device.BytesWrite);
        }
    }

    private void CalculateDiskRates(double elapsed)
    {
        if (elapsed <= 0)
        {
            SaveDiskCounters();
            return;
        }

        foreach (var device in diskStat.Devices)
        {
            if (prevDiskCounters.TryGetValue(device.Name, out var prev))
            {
                var readDelta = device.BytesRead >= prev.BytesRead ? device.BytesRead - prev.BytesRead : 0;
                var writeDelta = device.BytesWrite >= prev.BytesWrite ? device.BytesWrite - prev.BytesWrite : 0;
                diskReadBytesPerSec[device.Name] = readDelta / elapsed;
                diskWriteBytesPerSec[device.Name] = writeDelta / elapsed;
            }
        }

        SaveDiskCounters();
    }

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    private void SaveNetworkCounters()
    {
        foreach (var iface in networkStat.Interfaces)
        {
            prevNetCounters[iface.Name] = new NetworkIfCounters(iface.RxBytes, iface.TxBytes);
        }
    }

    private void CalculateNetworkRates(double elapsed)
    {
        if (elapsed <= 0)
        {
            SaveNetworkCounters();
            return;
        }

        foreach (var iface in networkStat.Interfaces)
        {
            if (prevNetCounters.TryGetValue(iface.Name, out var prev))
            {
                var rxDelta = Math.Max(0L, (long)iface.RxBytes - prev.RxBytes);
                var txDelta = Math.Max(0L, (long)iface.TxBytes - prev.TxBytes);
                netRxBytesPerSec[iface.Name] = rxDelta / elapsed;
                netTxBytesPerSec[iface.Name] = txDelta / elapsed;
            }
        }

        SaveNetworkCounters();
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
        if (elapsed <= 0 || !powerStat.Supported)
        {
            SavePowerCounters();
            return;
        }

        powerCpuW = (powerStat.Cpu - prevPowerCpuJ) / elapsed;
        powerGpuW = (powerStat.Gpu - prevPowerGpuJ) / elapsed;
        powerAneW = (powerStat.Ane - prevPowerAneJ) / elapsed;
        powerRamW = (powerStat.Ram - prevPowerRamJ) / elapsed;
        powerPciW = (powerStat.Pci - prevPowerPciJ) / elapsed;

        SavePowerCounters();
    }
}
