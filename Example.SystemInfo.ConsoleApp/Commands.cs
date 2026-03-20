// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable MemberCanBePrivate.Global
namespace Example.SystemInfo.ConsoleApp;

using System.Collections.Generic;

using MacDotNet.SystemInfo;

using Smart.CommandLine.Hosting;

public static class CommandBuilderExtensions
{
    public static void AddCommands(this ICommandBuilder commands)
    {
        commands.AddCommand<HardwareCommand>();
        commands.AddCommand<KernelCommand>();
        commands.AddCommand<UptimeCommand>();
        commands.AddCommand<LoadCommand>();
        commands.AddCommand<MemoryCommand>();
        commands.AddCommand<SwapCommand>();
        commands.AddCommand<DiskCommand>();
        commands.AddCommand<FileSystemCommand>();
        commands.AddCommand<NetworkCommand>();
        commands.AddCommand<ProcessCommand>();
        commands.AddCommand<ProcessesCommand>();
        commands.AddCommand<CpuCommand>();
        commands.AddCommand<CpuFrequencyCommand>();
        commands.AddCommand<GpuCommand>();
        commands.AddCommand<PowerCommand>();
        commands.AddCommand<SensorCommand>();
        commands.AddCommand<SummaryCommand>();
    }
}

public static class DisplayFormatter
{
    public static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1UL << 40 => $"{bytes / (double)(1UL << 40):F2} TB",
        >= 1UL << 30 => $"{bytes / (double)(1UL << 30):F2} GB",
        >= 1UL << 20 => $"{bytes / (double)(1UL << 20):F2} MB",
        >= 1UL << 10 => $"{bytes / (double)(1UL << 10):F2} KB",
        _ => $"{bytes} B"
    };

    public static string MakeBar(double value, double max, int width = 20)
    {
        ReadOnlySpan<char> partials = ['\u258f', '\u258e', '\u258d', '\u258c', '\u258b', '\u258a', '\u2589'];

        var ratio = max > 0 ? Math.Clamp(value / max, 0.0, 1.0) : 0.0;
        var totalEighths = (int)Math.Round(ratio * width * 8);
        var fullCells = totalEighths / 8;
        var remainder = totalEighths % 8;

        var buf = new char[width + 2];
        buf[0] = '[';
        buf[width + 1] = ']';

        var pos = 1;
        for (var i = 0; i < fullCells; i++)
        {
            buf[pos++] = '\u2588';
        }

        if (remainder > 0 && fullCells < width)
        {
            buf[pos++] = partials[remainder - 1];
        }

        while (pos <= width)
        {
            buf[pos++] = ' ';
        }

        return new string(buf);
    }
}

//--------------------------------------------------------------------------------
// Hardware
//--------------------------------------------------------------------------------
[Command("hardware", "Get hardware information")]
public sealed class HardwareCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var hw = PlatformProvider.GetHardware();

        Console.WriteLine("[System]");
        Console.WriteLine($"  Model:             {hw.Model}");
        Console.WriteLine($"  Machine:           {hw.Machine}");
        Console.WriteLine($"  TargetType:        {hw.TargetType}");
        Console.WriteLine($"  SerialNumber:      {hw.SerialNumber}");

        Console.WriteLine("[CPU]");
        Console.WriteLine($"  CpuBrand:          {hw.CpuBrandString}");
        Console.WriteLine($"  PhysicalCpu:       {hw.PhysicalCpu} (max: {hw.PhysicalCpuMax})");
        Console.WriteLine($"  LogicalCpu:        {hw.LogicalCpu} (max: {hw.LogicalCpuMax})");
        Console.WriteLine($"  ActiveCpu:         {hw.ActiveCpu}");
        Console.WriteLine($"  CoreCount:         {hw.CpuCoreCount}");
        Console.WriteLine($"  ThreadCount:       {hw.CpuThreadCount}");
        Console.WriteLine($"  Packages:          {hw.Packages}");
        Console.WriteLine($"  TimebaseFrequency: {hw.TimebaseFrequency} Hz");

        Console.WriteLine("[Memory]");
        Console.WriteLine($"  MemorySize:        {DisplayFormatter.FormatBytes((ulong)hw.MemorySize)}");
        Console.WriteLine($"  PageSize:          {hw.PageSize} bytes");

        Console.WriteLine("[Cache]");
        Console.WriteLine($"  CacheLineSize:     {hw.CacheLineSize} bytes");
        Console.WriteLine($"  L1I:               {DisplayFormatter.FormatBytes((ulong)hw.L1ICacheSize)}");
        Console.WriteLine($"  L1D:               {DisplayFormatter.FormatBytes((ulong)hw.L1DCacheSize)}");
        Console.WriteLine($"  L2:                {DisplayFormatter.FormatBytes((ulong)hw.L2CacheSize)}");
        Console.WriteLine($"  L3:                {DisplayFormatter.FormatBytes((ulong)hw.L3CacheSize)}");

        if (hw.PerformanceCoreCount > 0)
        {
            Console.WriteLine("[CPU Cores]");
            var pCore = hw.PerformanceCoreLevel;
            var freqStr = pCore.CpuFrequencyMax > 0 ? $", {pCore.CpuFrequencyMax / 1_000_000}MHz" : string.Empty;
            Console.WriteLine($"  P-Core ({pCore.Name}): {pCore.PhysicalCpu} physical, {pCore.LogicalCpu} logical, L2={DisplayFormatter.FormatBytes((ulong)pCore.L2CacheSize)}{freqStr}");
            if (hw.EfficiencyCoreCount > 0)
            {
                var eCore = hw.EfficiencyCoreLevel;
                freqStr = eCore.CpuFrequencyMax > 0 ? $", {eCore.CpuFrequencyMax / 1_000_000}MHz" : string.Empty;
                Console.WriteLine($"  E-Core ({eCore.Name}): {eCore.PhysicalCpu} physical, {eCore.LogicalCpu} logical, L2={DisplayFormatter.FormatBytes((ulong)eCore.L2CacheSize)}{freqStr}");
            }
        }

        if (hw.Gpus.Count > 0)
        {
            Console.WriteLine("[GPU]");
            foreach (var gpu in hw.Gpus)
            {
                Console.WriteLine($"  Model:           {gpu.Model}");
                Console.WriteLine($"  Name:            {gpu.Name}");
                Console.WriteLine($"  CoreCount:       {gpu.CoreCount}");
                Console.WriteLine($"  VendorId:        0x{gpu.VendorId:X}");
                Console.WriteLine($"  MetalPlugin:     {gpu.MetalPluginName}");
                if (gpu.GpuGeneration > 0)
                {
                    Console.WriteLine($"  GpuGeneration:   {gpu.GpuGeneration}");
                    Console.WriteLine($"  NumCores:        {gpu.NumCores}");
                    Console.WriteLine($"  NumGPs:          {gpu.NumGPs}");
                    Console.WriteLine($"  NumFragments:    {gpu.NumFragments}");
                }
            }
        }

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Kernel
//--------------------------------------------------------------------------------
[Command("kernel", "Get kernel information")]
public sealed class KernelCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var kernel = PlatformProvider.GetKernel();
        Console.WriteLine($"OsType:              {kernel.OsType}");
        Console.WriteLine($"OsRelease:           {kernel.OsRelease}");
        Console.WriteLine($"OsVersion:           {kernel.OsVersion}");
        Console.WriteLine($"OsProductVersion:    {kernel.OsProductVersion}");
        Console.WriteLine($"OsRevision:          {kernel.OsRevision}");
        Console.WriteLine($"KernelVersion:       {kernel.KernelVersion}");
        Console.WriteLine($"Uuid:                {kernel.Uuid}");
        Console.WriteLine($"MaxProcesses:        {kernel.MaxProcesses}");
        Console.WriteLine($"MaxProcessesPerUser: {kernel.MaxProcessesPerUser}");
        Console.WriteLine($"MaxFiles:            {kernel.MaxFiles}");
        Console.WriteLine($"MaxFilesPerProcess:  {kernel.MaxFilesPerProcess}");
        Console.WriteLine($"MaxArguments:        {kernel.MaxArguments}");
        Console.WriteLine($"SecureLevel:         {kernel.SecureLevel}");
        Console.WriteLine($"BootTime:            {kernel.BootTime:yyyy-MM-dd HH:mm:ss zzz}");

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Uptime
//--------------------------------------------------------------------------------
[Command("uptime", "Get uptime")]
public sealed class UptimeCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var uptime = PlatformProvider.GetUptime();
        var elapsed = uptime.Elapsed;
        Console.WriteLine($"Uptime: {(int)elapsed.TotalDays}d {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}");

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// LoadAverage
//--------------------------------------------------------------------------------
[Command("load", "Get load average")]
public sealed class LoadCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var cpuCount = Environment.ProcessorCount;

        using var cts = new CancellationTokenSource();
#pragma warning disable SA1107
        // ReSharper disable once AccessToDisposedClosure
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
#pragma warning restore SA1107

        while (!cts.Token.IsCancellationRequested)
        {
            var load = PlatformProvider.GetLoadAverage();

            Console.Clear();
            Console.WriteLine($"Load Average (CPUs: {cpuCount})");
            Console.WriteLine($"   1 min: {DisplayFormatter.MakeBar(load.Average1, cpuCount)} {load.Average1:F2}");
            Console.WriteLine($"   5 min: {DisplayFormatter.MakeBar(load.Average5, cpuCount)} {load.Average5:F2}");
            Console.WriteLine($"  15 min: {DisplayFormatter.MakeBar(load.Average15, cpuCount)} {load.Average15:F2}");

            await Task.Delay(2000, cts.Token);
        }
    }
}

//--------------------------------------------------------------------------------
// Memory
//--------------------------------------------------------------------------------
[Command("memory", "Get memory stat")]
public sealed class MemoryCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var mem = PlatformProvider.GetMemoryStat();
        var usage = mem.PhysicalMemory > 0 ? (double)mem.UsedBytes / mem.PhysicalMemory * 100 : 0;

        Console.WriteLine("[Usage]");
        Console.WriteLine($"  Total:         {DisplayFormatter.FormatBytes(mem.PhysicalMemory)}");
        Console.WriteLine($"  Used:          {DisplayFormatter.FormatBytes(mem.UsedBytes)} ({usage:F1}%)");
        Console.WriteLine($"  Free:          {DisplayFormatter.FormatBytes(mem.FreeBytes)}");

        Console.WriteLine("[Breakdown]");
        Console.WriteLine($"  Active:        {DisplayFormatter.FormatBytes(mem.ActiveBytes)}  ({mem.ActiveCount} pages)");
        Console.WriteLine($"  Inactive:      {DisplayFormatter.FormatBytes(mem.InactiveBytes)}  ({mem.InactiveCount} pages)");
        Console.WriteLine($"  Wired:         {DisplayFormatter.FormatBytes(mem.WiredBytes)}  ({mem.WireCount} pages)");
        Console.WriteLine($"  AppMemory:     {DisplayFormatter.FormatBytes(mem.AppMemoryBytes)}");
        Console.WriteLine($"  Compressed:    {DisplayFormatter.FormatBytes(mem.CompressorBytes)}  ({mem.CompressorPageCount} pages)");

        Console.WriteLine("[Compressor]");
        var compressionRatio = mem.CompressorPageCount > 0 ? (double)mem.TotalUncompressedPagesInCompressor / mem.CompressorPageCount : 0;
        Console.WriteLine($"  Ratio:         {compressionRatio:F2}x");
        Console.WriteLine($"  Compressions:  {mem.Compression}");
        Console.WriteLine($"  Decompression: {mem.Decompression}");

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Swap
//--------------------------------------------------------------------------------
[Command("swap", "Get swap usage")]
public sealed class SwapCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var swap = PlatformProvider.GetSwapUsage();
        var usage = swap.TotalBytes > 0 ? (double)swap.UsedBytes / swap.TotalBytes * 100 : 0;
        Console.WriteLine($"Total:     {DisplayFormatter.FormatBytes(swap.TotalBytes)}");
        Console.WriteLine($"Used:      {DisplayFormatter.FormatBytes(swap.UsedBytes)} ({usage:F1}%)");
        Console.WriteLine($"Available: {DisplayFormatter.FormatBytes(swap.AvailableBytes)}");
        Console.WriteLine($"Encrypted: {swap.IsEncrypted}");

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Disk
//--------------------------------------------------------------------------------
[Command("disk", "Get disk stat")]
public sealed class DiskCommand : ICommandHandler
{
    [Option<bool>("--all", "-a", Description = "All")]
    public bool All { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        var diskStats = PlatformProvider.GetDiskStat();
        foreach (var d in diskStats.Devices.Where(x => All || x.IsPhysical))
        {
            var deviceLabel = d.MediaName is not null ? $"{d.BsdName} [{d.MediaName}]" : d.BsdName;
            Console.WriteLine($"[Device] {deviceLabel}");
            if (d.VendorName is not null)
            {
                Console.WriteLine($"  Vendor:          {d.VendorName}");
            }
            if (d.MediumType is not null)
            {
                Console.WriteLine($"  MediumType:      {d.MediumType}");
            }
            Console.WriteLine($"  BusType:         {d.BusType}");
            Console.WriteLine($"  IsPhysical:      {d.IsPhysical}");
            Console.WriteLine($"  IsRemovable:     {d.IsRemovable}");
            Console.WriteLine($"  DiskSize:        {DisplayFormatter.FormatBytes(d.DiskSize)}");
            Console.WriteLine($"  BytesRead:       {DisplayFormatter.FormatBytes(d.BytesRead)}");
            Console.WriteLine($"  BytesWrite:      {DisplayFormatter.FormatBytes(d.BytesWrite)}");
            Console.WriteLine($"  ReadsCompleted:  {d.ReadsCompleted}");
            Console.WriteLine($"  WritesCompleted: {d.WritesCompleted}");
            Console.WriteLine($"  TimeRead:        {d.TotalTimeRead / 1_000_000} ms");
            Console.WriteLine($"  TimeWrite:       {d.TotalTimeWrite / 1_000_000} ms");
            Console.WriteLine($"  ErrorsRead:      {d.ErrorsRead}");
            Console.WriteLine($"  ErrorsWrite:     {d.ErrorsWrite}");
        }

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// FileSystem
//--------------------------------------------------------------------------------
[Command("fs", "Get file system info")]
public sealed class FileSystemCommand : ICommandHandler
{
    [Option<bool>("--all", "-a", Description = "All")]
    public bool All { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        var fsStat = PlatformProvider.GetFileSystemStat(All);
        foreach (var fs in fsStat.Entries)
        {
            var usedSize = fs.TotalSize > fs.AvailableSize ? fs.TotalSize - fs.AvailableSize : 0;
            var usage = fs.TotalSize > 0 ? usedSize * 100.0 / fs.TotalSize : 0;

            Console.WriteLine($"[MountPoint] {fs.MountPoint}");
            Console.WriteLine($"  DeviceName:    {fs.DeviceName}");
            Console.WriteLine($"  FileSystem:    {fs.FileSystem}");
            Console.WriteLine($"  Option:        {fs.Option}");
            Console.WriteLine($"  BlockSize:     {fs.BlockSize}");
            Console.WriteLine($"  IOSize:        {fs.IOSize}");
            Console.WriteLine($"  Usage:         {usage:F1}% ({DisplayFormatter.FormatBytes(usedSize)} / {DisplayFormatter.FormatBytes(fs.TotalSize)})");
            Console.WriteLine($"  AvailableSize: {DisplayFormatter.FormatBytes(fs.AvailableSize)}");
            Console.WriteLine($"  TotalFiles:    {fs.TotalFiles}");
            Console.WriteLine($"  FreeFiles:     {fs.FreeFiles}");
        }

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Process
//--------------------------------------------------------------------------------
[Command("process", "Get process summary")]
public sealed class ProcessCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var ps = PlatformProvider.GetProcessSummary();
        Console.WriteLine($"Process Count: {ps.ProcessCount}");
        Console.WriteLine($"Thread Count:  {ps.ThreadCount}");

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Processes
//--------------------------------------------------------------------------------
[Command("processes", "Get all processes")]
public sealed class ProcessesCommand : ICommandHandler
{
    [Option<int>("--top", "-t", Description = "Top", DefaultValue = 100)]
    public int Top { get; set; }

    [Option<string>("--sort", "-s", Description = "Sort", Completions = ["pid", "name", "cpu", "memory"], DefaultValue = "pid")]
    public string Sort { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context)
    {
        var processes = PlatformProvider.GetProcesses();

#pragma warning disable CA1308
        var sorted = Sort.ToLowerInvariant() switch
        {
            "name" => processes.OrderBy(p => p.Name),
            "cpu" => processes.OrderByDescending(p => p.UserTime + p.SystemTime),
            "memory" => processes.OrderByDescending(p => p.ResidentMemorySize),
            _ => processes.OrderBy(p => p.ProcessId)
        };
#pragma warning restore CA1308

        var topProcesses = sorted.Take(Top).ToList();

        Console.WriteLine($"{"PID",-6} {"Name",-20} {"State",-12} {"User",-5} {"Threads",7} {"RSS (MB)",10} {"CPU Time",10}");
        Console.WriteLine(new string('-', 76));

        foreach (var p in topProcesses)
        {
            var rss = (double)p.ResidentMemorySize / 1024 / 1024;
            var cpuTime = (p.UserTime + p.SystemTime).TotalSeconds;

            Console.WriteLine($"{p.ProcessId,-6} {TruncateName(p.Name, 20),-20} {p.Status,-12} {p.UserId,-5} {p.ThreadCount,7} {rss,10:F2} {cpuTime,10:F2}");
        }

        Console.WriteLine($"Total processes: {processes.Count}");

        return ValueTask.CompletedTask;
    }

    private static string TruncateName(string name, int maxLength)
    {
        return name.Length <= maxLength ? name : name[..(maxLength - 3)] + "...";
    }
}

//--------------------------------------------------------------------------------
// Network
//--------------------------------------------------------------------------------
[Command("network", "Get network stat")]
public sealed class NetworkCommand : ICommandHandler
{
    [Option<bool>("--all", "-a", Description = "All")]
    public bool All { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var network = PlatformProvider.GetNetworkStat(All);

        using var cts = new CancellationTokenSource();
#pragma warning disable SA1107
        // ReSharper disable once AccessToDisposedClosure
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
#pragma warning restore SA1107

        while (!cts.Token.IsCancellationRequested)
        {
            var snapshot = network.Interfaces
                .Where(static x => x.IsEnabled)
                .ToDictionary(x => x.Name, x => (x.RxBytes, x.RxPackets, x.RxErrors, x.TxBytes, x.TxPackets, x.TxErrors));
            var t0 = DateTime.UtcNow;

            await Task.Delay(1000, cts.Token);

            network.Update();
            var elapsed = (DateTime.UtcNow - t0).TotalSeconds;

            Console.Clear();
            foreach (var nif in network.Interfaces.Where(static x => x.IsEnabled))
            {
                var label = nif.DisplayName is not null ? $" {nif.DisplayName}" : string.Empty;
                Console.WriteLine($"[{nif.Name}]{label} ({nif.InterfaceType})");

                snapshot.TryGetValue(nif.Name, out var prev);
                var deltaRxBytes   = unchecked(nif.RxBytes   - prev.RxBytes);
                var deltaRxPackets = unchecked(nif.RxPackets - prev.RxPackets);
                var deltaRxErrors  = unchecked(nif.RxErrors  - prev.RxErrors);
                var deltaTxBytes   = unchecked(nif.TxBytes   - prev.TxBytes);
                var deltaTxPackets = unchecked(nif.TxPackets - prev.TxPackets);
                var deltaTxErrors  = unchecked(nif.TxErrors  - prev.TxErrors);

                var rxKbps = elapsed > 0 ? deltaRxBytes / 1024.0 / elapsed : 0;
                var txKbps = elapsed > 0 ? deltaTxBytes / 1024.0 / elapsed : 0;

                Console.WriteLine($"  RX: {DisplayFormatter.FormatBytes(nif.RxBytes),10} total  {rxKbps,8:F2} KB/s  ({deltaRxPackets} packet/s, {deltaRxErrors} error)");
                Console.WriteLine($"  TX: {DisplayFormatter.FormatBytes(nif.TxBytes),10} total  {txKbps,8:F2} KB/s  ({deltaTxPackets} packet/s, {deltaTxErrors} error)");
            }
        }
    }
}

//--------------------------------------------------------------------------------
// Cpu
//--------------------------------------------------------------------------------
[Command("cpu", "Get cpu stat")]
public sealed class CpuCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var stat = PlatformProvider.GetCpuStat();
        var header = $"Cores: {stat.CpuCores.Count} total  P-Core: {stat.PerformanceCores.Count}  E-Core: {stat.EfficiencyCores.Count}";

        using var cts = new CancellationTokenSource();
#pragma warning disable SA1107
        // ReSharper disable once AccessToDisposedClosure
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
#pragma warning restore SA1107

        while (!cts.Token.IsCancellationRequested)
        {
            var prevTotal              = CreateSnapshotFromCores(stat.CpuCores);
            var prevEfficiencyTotal    = CreateSnapshotFromCores(stat.EfficiencyCores);
            var prevPerformanceTotal   = CreateSnapshotFromCores(stat.PerformanceCores);
            var prevCoreValues         = stat.CpuCores.Select(CreateSnapshot).ToList();

            await Task.Delay(1000, cts.Token);

            stat.Update();

            var totalUsage       = CalcUsageOfCores(stat.CpuCores, prevTotal);
            var efficiencyUsage  = CalcUsageOfCores(stat.EfficiencyCores, prevEfficiencyTotal);
            var performanceUsage = CalcUsageOfCores(stat.PerformanceCores, prevPerformanceTotal);

            Console.Clear();
            Console.WriteLine(header);
            Console.WriteLine($"Total:   {DisplayFormatter.MakeBar(totalUsage, 100)} {totalUsage,3}%");
            Console.WriteLine($"E-Core:  {DisplayFormatter.MakeBar(efficiencyUsage, 100)} {efficiencyUsage,3}%");
            Console.WriteLine($"P-Core:  {DisplayFormatter.MakeBar(performanceUsage, 100)} {performanceUsage,3}%");

            Console.WriteLine("[E-Core]");
            for (var j = 0; j < stat.CpuCores.Count; j++)
            {
                if (stat.CpuCores[j].CoreType != CpuCoreType.Efficiency)
                {
                    continue;
                }
                var usage = CalcUsage(stat.CpuCores[j], prevCoreValues[j]);
                Console.WriteLine($"  cpu{stat.CpuCores[j].Number,2}: {DisplayFormatter.MakeBar(usage, 100)} {usage,3}%");
            }

            Console.WriteLine("[P-Core]");
            for (var j = 0; j < stat.CpuCores.Count; j++)
            {
                if (stat.CpuCores[j].CoreType != CpuCoreType.Performance)
                {
                    continue;
                }
                var usage = CalcUsage(stat.CpuCores[j], prevCoreValues[j]);
                Console.WriteLine($"  cpu{stat.CpuCores[j].Number,2}: {DisplayFormatter.MakeBar(usage, 100)} {usage,3}%");
            }
        }

        return;

        static long CalcCpuIdle(CpuCoreStat cpu) => cpu.Idle;

        static long CalcCpuIdleOfCores(IEnumerable<CpuCoreStat> cores) =>
            cores.Sum(static cpu => cpu.Idle);

        static long CalcCpuTotal(CpuCoreStat cpu) =>
            cpu.User + cpu.Nice + cpu.System + cpu.Idle;

        static long CalcCpuTotalOfCores(IEnumerable<CpuCoreStat> cores) =>
            cores.Sum(static cpu => (long)cpu.User + cpu.Nice + cpu.System + cpu.Idle);

        static (long Idle, long Total) CreateSnapshot(CpuCoreStat cpu) =>
            (CalcCpuIdle(cpu), CalcCpuTotal(cpu));

        static (long Idle, long Total) CreateSnapshotFromCores(IReadOnlyList<CpuCoreStat> cores) =>
            (CalcCpuIdleOfCores(cores), CalcCpuTotalOfCores(cores));

        static int CalcUsage(CpuCoreStat cpu, (long Idle, long Total) previous)
        {
            var idle = CalcCpuIdle(cpu);
            var total = CalcCpuTotal(cpu);
            var idleDiff = idle  - previous.Idle;
            var totalDiff = total - previous.Total;
            return totalDiff > 0 ? (int)Math.Ceiling((double)(totalDiff - idleDiff) / totalDiff * 100d) : 0;
        }

        static int CalcUsageOfCores(IReadOnlyList<CpuCoreStat> cores, (long Idle, long Total) previous)
        {
            var idle = CalcCpuIdleOfCores(cores);
            var total = CalcCpuTotalOfCores(cores);
            var idleDiff = idle  - previous.Idle;
            var totalDiff = total - previous.Total;
            return totalDiff > 0 ? (int)Math.Ceiling((double)(totalDiff - idleDiff) / totalDiff * 100d) : 0;
        }
    }
}

//--------------------------------------------------------------------------------
// CPU Frequency
//--------------------------------------------------------------------------------
// ReSharper disable once StringLiteralTypo
[Command("cpufreq", "Get cpu frequency")]
public sealed class CpuFrequencyCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var cpuFreq = PlatformProvider.GetCpuFrequency();
        var header1 = $"Max E-Core: {cpuFreq.MaxEfficiencyCoreFrequency} MHz  |  Max P-Core: {cpuFreq.MaxPerformanceCoreFrequency} MHz";
        var header2 = $"Cores: {cpuFreq.Cores.Count}  (E-Core: {cpuFreq.EfficiencyCores.Count}, P-Core: {cpuFreq.PerformanceCores.Count})";

        using var cts = new CancellationTokenSource();
#pragma warning disable SA1107
        // ReSharper disable once AccessToDisposedClosure
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
#pragma warning restore SA1107

        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(1000, cts.Token);

            cpuFreq.Update();

            Console.Clear();
            Console.WriteLine(header1);
            Console.WriteLine(header2);

            Console.WriteLine("[E-Core]");
            foreach (var core in cpuFreq.EfficiencyCores)
            {
                Console.WriteLine($"  E{core.Number,2}: {DisplayFormatter.MakeBar(core.Frequency, cpuFreq.MaxEfficiencyCoreFrequency)} {core.Frequency,7:F1} MHz ({core.Frequency / 1000.0:F2} GHz)");
            }

            var efficiencyAvg = cpuFreq.EfficiencyCores.Average(static c => c.Frequency);
            Console.WriteLine($"  Avg: {DisplayFormatter.MakeBar(efficiencyAvg, cpuFreq.MaxEfficiencyCoreFrequency)} {efficiencyAvg,7:F1} MHz");

            Console.WriteLine("[P-Core]");
            foreach (var core in cpuFreq.PerformanceCores)
            {
                Console.WriteLine($"  P{core.Number,2}: {DisplayFormatter.MakeBar(core.Frequency, cpuFreq.MaxPerformanceCoreFrequency)} {core.Frequency,7:F1} MHz ({core.Frequency / 1000.0:F2} GHz)");
            }

            var performanceAvg = cpuFreq.PerformanceCores.Average(static c => c.Frequency);
            Console.WriteLine($"  Avg: {DisplayFormatter.MakeBar(performanceAvg, cpuFreq.MaxPerformanceCoreFrequency)} {performanceAvg,7:F1} MHz");
        }
    }
}

//--------------------------------------------------------------------------------
// GPU
//--------------------------------------------------------------------------------
[Command("gpu", "Get gpu stat")]
public sealed class GpuCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        using var cts = new CancellationTokenSource();
#pragma warning disable SA1107
        // ReSharper disable once AccessToDisposedClosure
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
#pragma warning restore SA1107

        while (!cts.Token.IsCancellationRequested)
        {
            var devices = PlatformProvider.GetGpuDevices();

            Console.Clear();
            if (devices.Count == 0)
            {
                Console.WriteLine("No GPU found.");
            }
            else
            {
                foreach (var device in devices)
                {
                    Console.WriteLine($"[Name] {device.Name}");
                    Console.WriteLine($"  DeviceUtilization:   {DisplayFormatter.MakeBar(device.DeviceUtilization, 100)} {device.DeviceUtilization,3}%");
                    Console.WriteLine($"  RendererUtilization: {DisplayFormatter.MakeBar(device.RendererUtilization, 100)} {device.RendererUtilization,3}%");
                    Console.WriteLine($"  TilerUtilization:    {DisplayFormatter.MakeBar(device.TilerUtilization, 100)} {device.TilerUtilization,3}%");
                    Console.WriteLine($"  AllocSystemMemory:   {DisplayFormatter.FormatBytes((ulong)device.AllocSystemMemory)}");
                    Console.WriteLine($"  InUseSystemMemory:   {DisplayFormatter.FormatBytes((ulong)device.InUseSystemMemory)}");
                    Console.WriteLine($"  Temperature:         {device.Temperature} C");
                    Console.WriteLine($"  FanSpeed:            {device.FanSpeed}%");
                    Console.WriteLine($"  CoreClock:           {device.CoreClock} MHz");
                    Console.WriteLine($"  MemoryClock:         {device.MemoryClock} MHz");
                    Console.WriteLine($"  PowerState:          {(device.PowerState ? "Active" : "Powered Off")}");
                }
            }

            await Task.Delay(1000, cts.Token);
        }
    }
}

//--------------------------------------------------------------------------------
// Power
//--------------------------------------------------------------------------------
[Command("power", "Get power info")]
public sealed class PowerCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var power = PlatformProvider.GetPowerStat();
        if (!power.Supported)
        {
            Console.WriteLine("Power reporting not supported.");
            return;
        }

        using var cts = new CancellationTokenSource();
#pragma warning disable SA1107
        // ReSharper disable once AccessToDisposedClosure
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
#pragma warning restore SA1107

        power.Update();

        while (!cts.Token.IsCancellationRequested)
        {
            var prevCpu = power.Cpu;
            var prevGpu = power.Gpu;
            var prevAne = power.Ane;
            var prevRam = power.Ram;
            var prevPci = power.Pci;
            var prevTotal = power.Total;
            var prevTime = DateTime.UtcNow;

            await Task.Delay(1000, cts.Token);

            power.Update();

            var elapsed = (DateTime.UtcNow - prevTime).TotalSeconds;
            var cpuW = (power.Cpu - prevCpu) / elapsed;
            var gpuW = (power.Gpu - prevGpu) / elapsed;
            var aneW = (power.Ane - prevAne) / elapsed;
            var ramW = (power.Ram - prevRam) / elapsed;
            var pciW = (power.Pci - prevPci) / elapsed;
            var totalW = (power.Total - prevTotal) / elapsed;

            Console.Clear();
            Console.WriteLine($"CPU:   {DisplayFormatter.MakeBar(cpuW, totalW)} {cpuW,6:F2} W");
            Console.WriteLine($"GPU:   {DisplayFormatter.MakeBar(gpuW, totalW)} {gpuW,6:F2} W");
            Console.WriteLine($"ANE:   {DisplayFormatter.MakeBar(aneW, totalW)} {aneW,6:F2} W");
            Console.WriteLine($"RAM:   {DisplayFormatter.MakeBar(ramW, totalW)} {ramW,6:F2} W");
            Console.WriteLine($"PCI:   {DisplayFormatter.MakeBar(pciW, totalW)} {pciW,6:F2} W");
            Console.WriteLine($"{"Total:",-29} {totalW,6:F2} W");
        }
    }
}

//--------------------------------------------------------------------------------
// Sensor
//--------------------------------------------------------------------------------
[Command("sensor", "Get SMC sensor readings")]
public sealed class SensorCommand : ICommandHandler
{
    [Option<bool>("--all", "-a", Description = "All sensors")]
    public bool All { get; set; }

    [Option<bool>("--temp", "-t", Description = "Temperature sensors")]
    public bool Temp { get; set; }

    [Option<bool>("--voltage", "-v", Description = "Voltage sensors")]
    public bool Voltage { get; set; }

    [Option<bool>("--power", "-p", Description = "Power sensors")]
    public bool Power { get; set; }

    [Option<bool>("--current", "-c", Description = "Current sensors")]
    public bool Current { get; set; }

    [Option<bool>("--fan", "-f", Description = "Fan sensors")]
    public bool Fan { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        var monitor = PlatformProvider.GetSmcMonitor();

        if (All || Temp)
        {
            ShowTemperatures(monitor);
        }
        if (All || Voltage)
        {
            ShowVoltages(monitor);
        }
        if (All || Power)
        {
            ShowPowers(monitor);
        }
        if (All || Current)
        {
            ShowCurrents(monitor);
        }
        if (All || Fan)
        {
            ShowFans(monitor);
        }

        return ValueTask.CompletedTask;
    }

    private static void ShowTemperatures(SmcMonitor monitor)
    {
        Console.WriteLine("[Temperature]");
        if (monitor.Temperatures.Count == 0)
        {
            Console.WriteLine("  No sensors found.");
        }
        else
        {
            const double maxTemp = 100.0;
            Console.WriteLine($"  {"Key",-6} {"Type",-5} {"Description",-25} {"Value",8}");
            Console.WriteLine($"  {new string('-', 47)}");
            foreach (var s in monitor.Temperatures)
            {
                Console.WriteLine($"  {s.Key,-6} {s.DataTypeString,-5} {s.Description,-25} {s.Value,6:F1} C {DisplayFormatter.MakeBar(s.Value, maxTemp, 12)}");
            }
            Console.WriteLine($"  Total: {monitor.Temperatures.Count} sensors");
        }
        Console.WriteLine();
    }

    private static void ShowVoltages(SmcMonitor monitor)
    {
        Console.WriteLine("[Voltage]");
        if (monitor.Voltages.Count == 0)
        {
            Console.WriteLine("  No sensors found.");
        }
        else
        {
            Console.WriteLine($"  {"Key",-6} {"Description",-25} {"Value",8}");
            Console.WriteLine($"  {new string('-', 41)}");
            foreach (var s in monitor.Voltages)
            {
                Console.WriteLine($"  {s.Key,-6} {s.Description,-25} {s.Value,6:F3} V");
            }
            Console.WriteLine($"  Total: {monitor.Voltages.Count} sensors");
        }
        Console.WriteLine();
    }

    private static void ShowPowers(SmcMonitor monitor)
    {
        Console.WriteLine("[Power]");
        if (monitor.Powers.Count == 0)
        {
            Console.WriteLine("  No sensors found.");
        }
        else
        {
            Console.WriteLine($"  {"Key",-6} {"Description",-25} {"Value",8}");
            Console.WriteLine($"  {new string('-', 41)}");
            foreach (var s in monitor.Powers)
            {
                Console.WriteLine($"  {s.Key,-6} {s.Description,-25} {s.Value,6:F2} W");
            }

            // ReSharper disable StringLiteralTypo
            var inputPower = monitor.Powers.FirstOrDefault(static x => x.Key == "PDTR")?.Value ?? 0;
            var systemPower = monitor.Powers.FirstOrDefault(static x => x.Key == "PSTR")?.Value ?? 0;
            // ReSharper restore StringLiteralTypo
            Console.WriteLine($"  Total Input Power : {inputPower,6:F2} W");
            Console.WriteLine($"  Total System Power: {systemPower,6:F2} W");
            Console.WriteLine($"  Total: {monitor.Powers.Count} sensors");
        }
        Console.WriteLine();
    }

    private static void ShowCurrents(SmcMonitor monitor)
    {
        Console.WriteLine("[Current]");
        if (monitor.Currents.Count == 0)
        {
            Console.WriteLine("  No sensors found.");
        }
        else
        {
            Console.WriteLine($"  {"Key",-6} {"Description",-25} {"Value",8}");
            Console.WriteLine($"  {new string('-', 41)}");
            foreach (var s in monitor.Currents)
            {
                Console.WriteLine($"  {s.Key,-6} {s.Description,-25} {s.Value,6:F3} A");
            }
            Console.WriteLine($"  Total: {monitor.Currents.Count} sensors");
        }
        Console.WriteLine();
    }

    private static void ShowFans(SmcMonitor monitor)
    {
        Console.WriteLine("[Fan]");
        if (monitor.Fans.Count == 0)
        {
            Console.WriteLine("  No fans found.");
        }
        else
        {
            Console.WriteLine($"  Fan count: {monitor.Fans.Count}");
            foreach (var fan in monitor.Fans)
            {
                Console.WriteLine($"  Fan {fan.Index}:");
                Console.WriteLine($"    Actual: {fan.ActualRpm,4:F0} RPM");
                Console.WriteLine($"    Min:    {fan.MinRpm,4:F0} RPM");
                Console.WriteLine($"    Max:    {fan.MaxRpm,4:F0} RPM");
                Console.WriteLine($"    Target: {fan.TargetRpm,4:F0} RPM");
            }
        }
        Console.WriteLine();
    }
}

//--------------------------------------------------------------------------------
// Summary
//--------------------------------------------------------------------------------
[Command("summary", "Get summary")]
public sealed class SummaryCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var lines = new List<(string Label, string Value)>();

        var monitor = new SystemMonitor();

        await Task.Delay(1000);
        monitor.Update();

        // CPU
        lines.Add(("CPU Usage:", $"Total: {monitor.CpuUsageTotal:F1} %  (E: {monitor.CpuUsageEfficiency:F1} %  P: {monitor.CpuUsagePerformance:F1} %)"));
        lines.Add(("CPU Usage Breakdown:", $"User: {monitor.CpuUserPercent:F1} %  System: {monitor.CpuSystemPercent:F1} %  Idle: {monitor.CpuIdlePercent:F1} %"));
        lines.Add(("CPU Frequency All:", $"{monitor.CpuFrequencyAllHz / 1_000_000.0:F0} MHz  (E: {monitor.CpuFrequencyEfficiencyHz / 1_000_000.0:F0} MHz  P: {monitor.CpuFrequencyPerformanceHz / 1_000_000.0:F0} MHz)"));
        // System
        lines.Add(("Uptime:", $"{monitor.Uptime:d\\.hh\\:mm\\:ss}"));
        lines.Add(("System:", $"Processes: {monitor.ProcessCount}  Threads: {monitor.ThreadCount}"));
        lines.Add(("Load Average:", $"{monitor.LoadAverage1:F2}  {monitor.LoadAverage5:F2}  {monitor.LoadAverage15:F2}  (1/5/15 min)"));
        // Memory
        lines.Add(("Memory Usage:", $"{monitor.MemoryUsagePercent:F1} %  (Active: {monitor.MemoryActivePercent:F1} %  Wired: {monitor.MemoryWiredPercent:F1} %  Compressor: {monitor.MemoryCompressorPercent:F1} %)"));
        lines.Add(("Swap Usage:", $"{monitor.SwapUsagePercent:F1} %"));
        // GPU
        foreach (var gpu in monitor.GpuDevices)
        {
            lines.Add(($"GPU [{gpu.Name}]:", $"Device: {gpu.DeviceUtilization} %  Renderer: {gpu.RendererUtilization} %  Tiler: {gpu.TilerUtilization} %"));
        }
        // Disk
        foreach (var disk in monitor.DiskDevices)
        {
            lines.Add(($"Disk {disk.Name} ({disk.BusType}):", $"Read: {disk.ReadBytesPerSec / 1024.0:F1} KB/s  Write: {disk.WriteBytesPerSec / 1024.0:F1} KB/s"));
        }
        // File System
        foreach (var fs in monitor.FileSystems)
        {
            var usage = (double)(fs.TotalSize - fs.AvailableSize) / fs.TotalSize * 100.0;
            lines.Add(($"FS {fs.MountPoint} ({fs.FileSystem}):", $"{usage:F1} %  ({fs.TotalSize / 1024 / 1024 / 1024} GB total)"));
        }
        // Network
        foreach (var net in monitor.NetworkInterfaces)
        {
            var name = net.DisplayName is not null ? $"{net.Name} ({net.DisplayName})" : net.Name;
            lines.Add(($"Net {name}:", $"DL: {net.RxBytesPerSec / 1024.0:F1} KB/s  UL: {net.TxBytesPerSec / 1024.0:F1} KB/s  Total RX: {net.RxBytes / 1024 / 1024} MB  TX: {net.TxBytes / 1024 / 1024} MB"));
        }
        // Temperature
        if (monitor.CpuTemperature is { } cpuTemp)
        {
            lines.Add(("Temp CPU:", $"{cpuTemp:F2} C"));
        }
        if (monitor.MainboardTemperature is { } mbTemp)
        {
            lines.Add(("Temp Mainboard:", $"{mbTemp:F2} C"));
        }
        if (monitor.NandTemperature is { } nandTemp)
        {
            lines.Add(("Temp NAND:", $"{nandTemp:F2} C"));
        }
        if (monitor.SsdTemperature is { } ssdTemp)
        {
            lines.Add(("Temp SSD:", $"{ssdTemp:F2} C"));
        }
        // Voltage
        if (monitor.DcInVoltage is { } voltage)
        {
            lines.Add(("Voltage DC-in:", $"{voltage:F3} V"));
        }
        // Current
        if (monitor.DcInCurrent is { } current)
        {
            lines.Add(("Current DC-in:", $"{current:F3} A"));
        }
        // Power
        if (monitor.DcInPower is { } dcPower)
        {
            lines.Add(("Power DC-in:", $"{dcPower:F2} W"));
        }
        if (monitor.TotalSystemPower is { } sysTotal)
        {
            lines.Add(("Power Total System:", $"{sysTotal:F2} W"));
        }
        // Fan
        foreach (var f in monitor.Fans)
        {
            var usage = f.MaxRpm > 0 ? f.ActualRpm / f.MaxRpm * 100.0 : 0;
            lines.Add(($"Fan {f.Index}:", $"{f.ActualRpm:F0} rpm  ({usage:F1} %)  [min: {f.MinRpm:F0}  max: {f.MaxRpm:F0}]"));
        }
        // Power Consumption
        lines.Add(("Power:", $"CPU: {monitor.PowerCpuW:F2} W  GPU: {monitor.PowerGpuW:F2} W  ANE: {monitor.PowerAneW:F2} W  RAM: {monitor.PowerRamW:F2} W  PCI: {monitor.PowerPciW:F2} W"));

        var labelWidth = lines.Max(l => l.Label.Length);
        foreach (var (label, value) in lines)
        {
            Console.WriteLine($"{label.PadRight(labelWidth)}  {value}");
        }
    }
}
