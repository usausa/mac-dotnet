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
        var filled = max > 0 ? (int)(value / max * width) : 0;
        filled = Math.Clamp(filled, 0, width);
        return "[" + new string('\u2588', filled) + new string('\u2591', width - filled) + "]";
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
                if (gpu.MetalPluginName.Length > 0)
                {
                    Console.WriteLine($"  MetalPlugin:     {gpu.MetalPluginName}");
                }
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

        Console.WriteLine();
        Console.WriteLine($"Total processes: {processes.Count}");

        return ValueTask.CompletedTask;
    }

    private static string TruncateName(string name, int maxLength)
    {
        return name.Length <= maxLength ? name : name[..(maxLength - 3)] + "...";
    }
}

//--------------------------------------------------------------------------------
// LoadAverage
//--------------------------------------------------------------------------------
[Command("load", "Get load average")]
public sealed class LoadCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var load = PlatformProvider.GetLoadAverage();
        var cpuCount = Environment.ProcessorCount;

        Console.WriteLine($"Load Average  (logical CPUs: {cpuCount})");
        Console.WriteLine();
        Console.WriteLine($"   1 min: {DisplayFormatter.MakeBar(load.Average1, cpuCount)} {load.Average1:F2}");
        Console.WriteLine($"   5 min: {DisplayFormatter.MakeBar(load.Average5, cpuCount)} {load.Average5:F2}");
        Console.WriteLine($"  15 min: {DisplayFormatter.MakeBar(load.Average15, cpuCount)} {load.Average15:F2}");

        return ValueTask.CompletedTask;
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
        var usagePct = mem.PhysicalMemory > 0 ? (double)mem.UsedBytes / mem.PhysicalMemory * 100 : 0;

        Console.WriteLine("[Usage]");
        Console.WriteLine($"  Total:         {DisplayFormatter.FormatBytes(mem.PhysicalMemory)}");
        Console.WriteLine($"  Used:          {DisplayFormatter.FormatBytes(mem.UsedBytes)} ({usagePct:F1}%)");
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
            var deviceLabel = d.MediaName is not null ? $"{d.Name} [{d.MediaName}]" : d.Name;
            Console.WriteLine($"Device:          {deviceLabel}");
            if (d.VendorName is not null)
            {
                Console.WriteLine($"Vendor:          {d.VendorName}");
            }
            if (d.MediumType is not null)
            {
                Console.WriteLine($"MediumType:      {d.MediumType}");
            }
            Console.WriteLine($"BusType:         {d.BusType}");
            Console.WriteLine($"IsPhysical:      {d.IsPhysical}");
            Console.WriteLine($"IsRemovable:     {d.IsRemovable}");
            Console.WriteLine($"DiskSize:        {DisplayFormatter.FormatBytes(d.DiskSize)}");
            Console.WriteLine($"BytesRead:       {DisplayFormatter.FormatBytes(d.BytesRead)}");
            Console.WriteLine($"BytesWrite:      {DisplayFormatter.FormatBytes(d.BytesWrite)}");
            Console.WriteLine($"ReadsCompleted:  {d.ReadsCompleted}");
            Console.WriteLine($"WritesCompleted: {d.WritesCompleted}");
            Console.WriteLine($"TimeRead:        {d.TotalTimeRead / 1_000_000} ms");
            Console.WriteLine($"TimeWrite:       {d.TotalTimeWrite / 1_000_000} ms");
            Console.WriteLine($"ErrorsRead:      {d.ErrorsRead}");
            Console.WriteLine($"ErrorsWrite:     {d.ErrorsWrite}");
            Console.WriteLine();
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
        var fileSystems = PlatformProvider.GetFileSystems(All);
        foreach (var fs in fileSystems)
        {
            var isReadOnly = fs.Option.HasFlag(MountOption.ReadOnly);
            var roMarker = isReadOnly ? " [RO]" : string.Empty;
            var usedSize = fs.TotalSize > fs.AvailableSize ? fs.TotalSize - fs.AvailableSize : 0;
            var usage = fs.TotalSize > 0 ? usedSize * 100.0 / fs.TotalSize : 0;

            Console.WriteLine($"MountPoint:    {fs.MountPoint}{roMarker}");
            Console.WriteLine($"DeviceName:    {fs.DeviceName}");
            Console.WriteLine($"FileSystem:    {fs.FileSystem}");
            Console.WriteLine($"Option:        {fs.Option}");
            Console.WriteLine($"BlockSize:     {fs.BlockSize}");
            Console.WriteLine($"IOSize:        {fs.IOSize}");
            Console.WriteLine($"Usage:         {DisplayFormatter.MakeBar(usage, 100)} {usage:F1}% ({DisplayFormatter.FormatBytes(usedSize)} / {DisplayFormatter.FormatBytes(fs.TotalSize)})");
            Console.WriteLine($"AvailableSize: {DisplayFormatter.FormatBytes(fs.AvailableSize)}");
            Console.WriteLine($"TotalFiles:    {fs.TotalFiles}");
            Console.WriteLine($"FreeFiles:     {fs.FreeFiles}");
            Console.WriteLine();
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
// Network
//--------------------------------------------------------------------------------
[Command("network", "Get network stat")]
public sealed class NetworkCommand : ICommandHandler
{
    [Option<bool>("--all", "-a", Description = "All")]
    public bool All { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        var network = PlatformProvider.GetNetworkStat(All);
        foreach (var nif in network.Interfaces.Where(static x => x.IsEnabled))
        {
            var name = nif.DisplayName is not null ? $"{nif.Name} {nif.DisplayName}" : nif.Name;
            Console.WriteLine($"Interface:    {name} ({nif.InterfaceType})");
            Console.WriteLine($"RxBytes:      {DisplayFormatter.FormatBytes(nif.RxBytes)}");
            Console.WriteLine($"RxPackets:    {nif.RxPackets:N0}");
            Console.WriteLine($"RxErrors:     {nif.RxErrors:N0}");
            Console.WriteLine($"RxDrops:      {nif.RxDrops:N0}");
            Console.WriteLine($"RxMulticast:  {nif.RxMulticast:N0}");
            Console.WriteLine($"TxBytes:      {DisplayFormatter.FormatBytes(nif.TxBytes)}");
            Console.WriteLine($"TxPackets:    {nif.TxPackets:N0}");
            Console.WriteLine($"TxErrors:     {nif.TxErrors:N0}");
            Console.WriteLine($"TxMulticast:  {nif.TxMulticast:N0}");
            Console.WriteLine($"Collisions:   {nif.Collisions:N0}");
            Console.WriteLine($"NoProto:      {nif.NoProto:N0}");
            Console.WriteLine();
        }

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Cpu
//--------------------------------------------------------------------------------
[Command("cpu", "Get cpu stat")]
public sealed class CpuCommand : ICommandHandler
{
    [Option<int>("--count", "-n", Description = "Number of samples", DefaultValue = 10)]
    public int Count { get; set; }

    [Option<bool>("--watch", "-w", Description = "Continuous watch mode (Ctrl+C to stop)")]
    public bool Watch { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var stat = PlatformProvider.GetCpuStat();
        var hasAppleSilicon = stat.EfficiencyCores.Count > 0 && stat.PerformanceCores.Count > 0;

        var header = $"Cores: {stat.CpuCores.Count} total" +
            (hasAppleSilicon ? $" (P-Core: {stat.PerformanceCores.Count}, E-Core: {stat.EfficiencyCores.Count})" : string.Empty);
        Console.WriteLine(header);
        Console.WriteLine();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var limit = Watch ? int.MaxValue : Count;
        for (var i = 0; i < limit && !cts.Token.IsCancellationRequested; i++)
        {
            var prevTotal = CreateSnapshotFromCores(stat.CpuCores);
            var prevETotal = CreateSnapshotFromCores(stat.EfficiencyCores);
            var prevPTotal = CreateSnapshotFromCores(stat.PerformanceCores);
            var prevCoreValues = stat.CpuCores.Select(CreateSnapshot).ToList();

            try { await Task.Delay(1000, cts.Token); }
            catch (OperationCanceledException) { break; }

            stat.Update();

            if (Watch)
            {
                Console.Clear();
                Console.WriteLine(header);
                Console.WriteLine();
            }

            var totalUsage = CalcUsageOfCores(stat.CpuCores, prevTotal);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]");
            Console.WriteLine($"  Total  {DisplayFormatter.MakeBar(totalUsage, 100)} {totalUsage,3}%");

            if (hasAppleSilicon)
            {
                var eUsage = CalcUsageOfCores(stat.EfficiencyCores, prevETotal);
                var pUsage = CalcUsageOfCores(stat.PerformanceCores, prevPTotal);
                Console.WriteLine($"  E-Core {DisplayFormatter.MakeBar(eUsage, 100)} {eUsage,3}%");
                Console.WriteLine($"  P-Core {DisplayFormatter.MakeBar(pUsage, 100)} {pUsage,3}%");
                Console.WriteLine();

                Console.WriteLine("  [P-Core]");
                for (var j = 0; j < stat.CpuCores.Count; j++)
                {
                    if (stat.CpuCores[j].CoreType != CpuCoreType.Performance) continue;
                    var usage = CalcUsage(stat.CpuCores[j], prevCoreValues[j]);
                    Console.WriteLine($"    cpu{stat.CpuCores[j].Number,2}: {DisplayFormatter.MakeBar(usage, 100, 16)} {usage,3}%");
                }

                Console.WriteLine("  [E-Core]");
                for (var j = 0; j < stat.CpuCores.Count; j++)
                {
                    if (stat.CpuCores[j].CoreType != CpuCoreType.Efficiency) continue;
                    var usage = CalcUsage(stat.CpuCores[j], prevCoreValues[j]);
                    Console.WriteLine($"    cpu{stat.CpuCores[j].Number,2}: {DisplayFormatter.MakeBar(usage, 100, 16)} {usage,3}%");
                }
            }
            else
            {
                Console.WriteLine();
                for (var j = 0; j < stat.CpuCores.Count; j++)
                {
                    var usage = CalcUsage(stat.CpuCores[j], prevCoreValues[j]);
                    Console.WriteLine($"  cpu{stat.CpuCores[j].Number,2}: {DisplayFormatter.MakeBar(usage, 100, 16)} {usage,3}%");
                }
            }

            Console.WriteLine();
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
            var idleDiff = idle - previous.Idle;
            var totalDiff = total - previous.Total;
            return totalDiff > 0 ? (int)Math.Ceiling((double)(totalDiff - idleDiff) / totalDiff * 100d) : 0;
        }

        static int CalcUsageOfCores(IReadOnlyList<CpuCoreStat> cores, (long Idle, long Total) previous)
        {
            var idle = CalcCpuIdleOfCores(cores);
            var total = CalcCpuTotalOfCores(cores);
            var idleDiff = idle - previous.Idle;
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
    [Option<int>("--count", "-c", Description = "Number of samples", DefaultValue = 5)]
    public int Count { get; set; }

    [Option<bool>("--watch", "-w", Description = "Continuous watch mode (Ctrl+C to stop)")]
    public bool Watch { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var cpuFreq = PlatformProvider.GetCpuFrequency();

        var header1 = $"Max E-Core: {cpuFreq.MaxEfficiencyCoreFrequency} MHz  |  Max P-Core: {cpuFreq.MaxPerformanceCoreFrequency} MHz";
        var header2 = $"Cores: {cpuFreq.Cores.Count} (E-Core: {cpuFreq.EfficiencyCores.Count}, P-Core: {cpuFreq.PerformanceCores.Count})";
        Console.WriteLine(header1);
        Console.WriteLine(header2);
        Console.WriteLine();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var limit = Watch ? int.MaxValue : Count;
        for (var i = 0; i < limit && !cts.Token.IsCancellationRequested; i++)
        {
            try { await Task.Delay(1000, cts.Token); }
            catch (OperationCanceledException) { break; }

            cpuFreq.Update();

            if (Watch)
            {
                Console.Clear();
                Console.WriteLine(header1);
                Console.WriteLine(header2);
                Console.WriteLine();
            }

            Console.WriteLine($"[Sample {i + 1}] {cpuFreq.UpdateAt:HH:mm:ss.fff}");

            if (cpuFreq.EfficiencyCores.Count > 0)
            {
                Console.WriteLine("  [E-Core]");
                foreach (var core in cpuFreq.EfficiencyCores)
                {
                    Console.WriteLine($"    E{core.Number,2}: {DisplayFormatter.MakeBar(core.Frequency, cpuFreq.MaxEfficiencyCoreFrequency, 16)} {core.Frequency,7:F1} MHz ({core.Frequency / 1000.0:F2} GHz)");
                }

                var eAvg = cpuFreq.EfficiencyCores.Average(static c => c.Frequency);
                Console.WriteLine($"    Avg: {DisplayFormatter.MakeBar(eAvg, cpuFreq.MaxEfficiencyCoreFrequency, 16)} {eAvg,7:F1} MHz");
            }

            if (cpuFreq.PerformanceCores.Count > 0)
            {
                Console.WriteLine("  [P-Core]");
                foreach (var core in cpuFreq.PerformanceCores)
                {
                    Console.WriteLine($"    P{core.Number,2}: {DisplayFormatter.MakeBar(core.Frequency, cpuFreq.MaxPerformanceCoreFrequency, 16)} {core.Frequency,7:F1} MHz ({core.Frequency / 1000.0:F2} GHz)");
                }

                var pAvg = cpuFreq.PerformanceCores.Average(static c => c.Frequency);
                Console.WriteLine($"    Avg: {DisplayFormatter.MakeBar(pAvg, cpuFreq.MaxPerformanceCoreFrequency, 16)} {pAvg,7:F1} MHz");
            }

            Console.WriteLine();
        }
    }
}

//--------------------------------------------------------------------------------
// GPU
//--------------------------------------------------------------------------------
[Command("gpu", "Get gpu stat")]
public sealed class GpuCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var devices = PlatformProvider.GetGpuDevices();
        if (devices.Count == 0)
        {
            Console.WriteLine("No GPU found.");
            return ValueTask.CompletedTask;
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            Console.WriteLine($"Name:                {device.Name}");
            Console.WriteLine($"DeviceUtilization:   {DisplayFormatter.MakeBar(device.DeviceUtilization, 100)} {device.DeviceUtilization}%");
            Console.WriteLine($"RendererUtilization: {DisplayFormatter.MakeBar(device.RendererUtilization, 100)} {device.RendererUtilization}%");
            Console.WriteLine($"TilerUtilization:    {DisplayFormatter.MakeBar(device.TilerUtilization, 100)} {device.TilerUtilization}%");
            Console.WriteLine($"AllocSystemMemory:   {DisplayFormatter.FormatBytes((ulong)device.AllocSystemMemory)}");
            Console.WriteLine($"InUseSystemMemory:   {DisplayFormatter.FormatBytes((ulong)device.InUseSystemMemory)}");
            Console.WriteLine($"Temperature:         {device.Temperature} C");
            Console.WriteLine($"FanSpeed:            {device.FanSpeed}%");
            Console.WriteLine($"CoreClock:           {device.CoreClock} MHz");
            Console.WriteLine($"MemoryClock:         {device.MemoryClock} MHz");
            Console.WriteLine($"PowerState:          {(device.PowerState ? "Active" : "Powered Off")}");
            Console.WriteLine();
        }

        return ValueTask.CompletedTask;
    }
}

//--------------------------------------------------------------------------------
// Power
//--------------------------------------------------------------------------------
[Command("power", "Get power info")]
public sealed class PowerCommand : ICommandHandler
{
    [Option<int>("--count", "-n", Description = "Number of samples", DefaultValue = 1)]
    public int Count { get; set; }

    [Option<bool>("--watch", "-w", Description = "Continuous watch mode (Ctrl+C to stop)")]
    public bool Watch { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var power = PlatformProvider.GetPowerStat();
        if (!power.Supported)
        {
            Console.WriteLine("Apple Silicon power reporting not supported (requires ARM64).");
            return;
        }

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // initial baseline snapshot
        power.Update();

        var limit = Watch ? int.MaxValue : Count;
        for (var i = 0; i < limit && !cts.Token.IsCancellationRequested; i++)
        {
            var prevCpu = power.Cpu;
            var prevGpu = power.Gpu;
            var prevAne = power.Ane;
            var prevRam = power.Ram;
            var prevPci = power.Pci;
            var prevTotal = power.Total;
            var prevTime = DateTime.UtcNow;

            try { await Task.Delay(1000, cts.Token); }
            catch (OperationCanceledException) { break; }

            power.Update();
            var elapsed = (DateTime.UtcNow - prevTime).TotalSeconds;

            var cpuW = (power.Cpu - prevCpu) / elapsed;
            var gpuW = (power.Gpu - prevGpu) / elapsed;
            var aneW = (power.Ane - prevAne) / elapsed;
            var ramW = (power.Ram - prevRam) / elapsed;
            var pciW = (power.Pci - prevPci) / elapsed;
            var totalW = (power.Total - prevTotal) / elapsed;

            if (Watch)
            {
                Console.Clear();
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]  elapsed: {elapsed:F2}s");
            Console.WriteLine($"  CPU   {DisplayFormatter.MakeBar(cpuW, totalW)} {cpuW,6:F2} W");
            Console.WriteLine($"  GPU   {DisplayFormatter.MakeBar(gpuW, totalW)} {gpuW,6:F2} W");
            Console.WriteLine($"  ANE   {DisplayFormatter.MakeBar(aneW, totalW)} {aneW,6:F2} W");
            Console.WriteLine($"  RAM   {DisplayFormatter.MakeBar(ramW, totalW)} {ramW,6:F2} W");
            Console.WriteLine($"  PCI   {DisplayFormatter.MakeBar(pciW, totalW)} {pciW,6:F2} W");
            Console.WriteLine($"  {"Total",-26} {totalW,6:F2} W");
            Console.WriteLine();
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
            const double maxTemp = 120.0;
            Console.WriteLine($"  {"Key",-6} {"Type",-5} {"Description",-45} {"Value",9}");
            Console.WriteLine($"  {new string('-', 72)}");
            foreach (var s in monitor.Temperatures)
            {
                Console.WriteLine($"  {s.Key,-6} {s.DataTypeString,-5} {s.Description,-45} {DisplayFormatter.MakeBar(s.Value, maxTemp, 12)} {s.Value,6:F1} \u00b0C");
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
            Console.WriteLine($"  {"Key",-6} {"Description",-45} {"Value",9}");
            Console.WriteLine($"  {new string('-', 62)}");
            foreach (var s in monitor.Voltages)
            {
                Console.WriteLine($"  {s.Key,-6} {s.Description,-45} {s.Value,8:F3} V");
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
            Console.WriteLine($"  {"Key",-6} {"Description",-45} {"Value",9}");
            Console.WriteLine($"  {new string('-', 62)}");
            foreach (var s in monitor.Powers)
            {
                Console.WriteLine($"  {s.Key,-6} {s.Description,-45} {s.Value,8:F2} W");
            }

            // ReSharper disable StringLiteralTypo
            var inputPower = monitor.Powers.FirstOrDefault(static x => x.Key == "PDTR")?.Value ?? 0;
            var systemPower = monitor.Powers.FirstOrDefault(static x => x.Key == "PSTR")?.Value ?? 0;
            // ReSharper restore StringLiteralTypo
            Console.WriteLine($"  Total Input Power : {inputPower:F2} W");
            Console.WriteLine($"  Total System Power: {systemPower:F2} W");
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
            Console.WriteLine($"  {"Key",-6} {"Description",-45} {"Value",9}");
            Console.WriteLine($"  {new string('-', 62)}");
            foreach (var s in monitor.Currents)
            {
                Console.WriteLine($"  {s.Key,-6} {s.Description,-45} {s.Value,8:F3} A");
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
                Console.WriteLine($"    Actual: {fan.ActualRpm,8:F0} RPM");
                Console.WriteLine($"    Min:    {fan.MinRpm,8:F0} RPM");
                Console.WriteLine($"    Max:    {fan.MaxRpm,8:F0} RPM");
                Console.WriteLine($"    Target: {fan.TargetRpm,8:F0} RPM");
            }
        }
        Console.WriteLine();
    }
}
