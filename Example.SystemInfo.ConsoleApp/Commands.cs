// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable MemberCanBePrivate.Global
namespace Example.SystemInfo.ConsoleApp;

using MacDotNet.SystemInfo;

using Smart.CommandLine.Hosting;

public static class CommandBuilderExtensions
{
    public static void AddCommands(this ICommandBuilder commands)
    {
        //commands.AddCommand<HardwareCommand>();
        commands.AddCommand<KernelCommand>();
        commands.AddCommand<UptimeCommand>();
        commands.AddCommand<CpuCommand>();
        commands.AddCommand<LoadCommand>();
        commands.AddCommand<MemoryCommand>();
        commands.AddCommand<SwapCommand>();
        commands.AddCommand<DiskCommand>();
        commands.AddCommand<FileSystemCommand>();
        commands.AddCommand<NetworkCommand>();
        commands.AddCommand<ProcessCommand>();
        commands.AddCommand<ProcessesCommand>();
        commands.AddCommand<GpuCommand>();
        commands.AddCommand<PowerCommand>();
        //commands.AddCommand<TemperatureCommand>();
        //commands.AddCommand<FanCommand>();
        //commands.AddCommand<VoltageCommand>();
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
}

// TODO
//// Hardware
//[Command("hardware", "Hardware Info")]
//public sealed class HardwareCommand : ICommandHandler
//{
//    public ValueTask ExecuteAsync(CommandContext context)
//    {
//        var hw = PlatformProvider.GetHardware();

//        Console.WriteLine("=== Hardware ===");
//        Console.WriteLine($"Model:            {hw.Model}");
//        Console.WriteLine($"Machine:          {hw.Machine}");
//        Console.WriteLine($"Serial Number:    {hw.SerialNumber ?? "(unavailable)"}");
//        Console.WriteLine($"CPU Brand:        {hw.CpuBrandString ?? "(unavailable)"}");
//        Console.WriteLine();

//        Console.WriteLine("=== CPU ===");
//        Console.WriteLine($"Physical CPU:     {hw.PhysicalCpu}");
//        Console.WriteLine($"Logical CPU:      {hw.LogicalCpu}");
//        Console.WriteLine($"Active CPU:       {hw.ActiveCpu}");
//        Console.WriteLine();

//        Console.WriteLine("=== Memory ===");
//        Console.WriteLine($"Physical Memory:  {FormatBytes((ulong)hw.MemSize)}");
//        Console.WriteLine($"Page Size:        {hw.PageSize} bytes");
//        Console.WriteLine();

//        var perfLevels = PlatformProvider.GetPerformanceLevels();
//        if (perfLevels.Count > 0)
//        {
//            Console.WriteLine("=== Performance Levels (Apple Silicon) ===");
//            foreach (var level in perfLevels)
//            {
//                var freqStr = level.CpuFrequencyMax > 0 ? $", {level.CpuFrequencyMax / 1_000_000}MHz" : "";
//                Console.WriteLine($"  [{level.Index}] {level.Name}: {level.PhysicalCpu} physical, {level.LogicalCpu} logical{freqStr}");
//            }
//        }

//        return ValueTask.CompletedTask;
//    }

//    private static string FormatBytes(ulong bytes) => bytes switch
//    {
//        >= 1UL << 40 => $"{bytes / (double)(1UL << 40):F2} TiB",
//        >= 1UL << 30 => $"{bytes / (double)(1UL << 30):F2} GiB",
//        >= 1UL << 20 => $"{bytes / (double)(1UL << 20):F2} MiB",
//        >= 1UL << 10 => $"{bytes / (double)(1UL << 10):F2} KiB",
//        _ => $"{bytes} B"
//    };
//}

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
        Console.WriteLine($"BootTime:            {kernel.BootTime}");

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
        Console.WriteLine($"Uptime: {uptime.Elapsed}");

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
// Cpu
//--------------------------------------------------------------------------------
[Command("cpu", "Get cpu stat")]
public sealed class CpuCommand : ICommandHandler
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var stat = PlatformProvider.GetCpuStat();

        Console.WriteLine($"User:   {stat.CpuTotal.User}");
        Console.WriteLine($"Nice:   {stat.CpuTotal.Nice}");
        Console.WriteLine($"System: {stat.CpuTotal.System}");
        Console.WriteLine($"Idle:   {stat.CpuTotal.Idle}");
        Console.WriteLine();

        for (var i = 0; i < 10; i++)
        {
            var previousValues = stat.CpuCores
                .Select(static x => new
                {
                    Idle = CalcCpuIdle(x),
                    Total = CalcCpuTotal(x)
                })
                .ToList();

            await Task.Delay(1000);

            stat.Update();

            for (var j = 0; j < stat.CpuCores.Count; j++)
            {
                var core = stat.CpuCores[j];
                var idle = CalcCpuIdle(core);
                var total = CalcCpuTotal(core);

                var idleDiff = idle - previousValues[j].Idle;
                var totalDiff = total - previousValues[j].Total;
                var usage = totalDiff > 0 ? (int)Math.Ceiling((double)(totalDiff - idleDiff) / totalDiff * 100d) : 0;

                Console.WriteLine($"Name:  cpu{core.Name}");
                Console.WriteLine($"Usage: {usage}");
            }

            Console.WriteLine();
        }

        static long CalcCpuIdle(CpuCoreStat cpu)
        {
            return cpu.Idle;
        }

        static long CalcCpuTotal(CpuCoreStat cpu)
        {
            return cpu.User + cpu.Nice + cpu.System + cpu.Idle;
        }
    }
}

// TODO
//// CPU Load (detailed)
//[Command("cpuload", "CPU Load Info (User/System/Idle, per core)")]
//public sealed class CpuLoadCommand : ICommandHandler
//{
//    public ValueTask ExecuteAsync(CommandContext context)
//    {
//        var cpuLoad = PlatformProvider.GetCpuLoad();

//        Console.WriteLine("=== CPU Load Info ===");
//        Console.WriteLine($"Logical CPUs:       {cpuLoad.LogicalCpu}");
//        Console.WriteLine($"Physical CPUs:      {cpuLoad.PhysicalCpu}");
//        Console.WriteLine($"Hyperthreading:     {cpuLoad.HasHyperthreading}");
//        Console.WriteLine();

//        cpuLoad.Update();
//        Thread.Sleep(500);
//        cpuLoad.Update();

//        Console.WriteLine("=== Usage ===");
//        Console.WriteLine($"User Load:          {cpuLoad.UserLoad:P1}");
//        Console.WriteLine($"System Load:        {cpuLoad.SystemLoad:P1}");
//        Console.WriteLine($"Idle Load:          {cpuLoad.IdleLoad:P1}");
//        Console.WriteLine($"Total Load:         {cpuLoad.TotalLoad:P1}");
//        Console.WriteLine();

//        if (cpuLoad.ECoreUsage.HasValue || cpuLoad.PCoreUsage.HasValue)
//        {
//            Console.WriteLine("=== Apple Silicon Core Usage ===");
//            if (cpuLoad.ECoreUsage.HasValue)
//            {
//                Console.WriteLine($"E-Core Usage:       {cpuLoad.ECoreUsage:P1}");
//            }

//            if (cpuLoad.PCoreUsage.HasValue)
//            {
//                Console.WriteLine($"P-Core Usage:       {cpuLoad.PCoreUsage:P1}");
//            }

//            Console.WriteLine();
//        }

//        Console.WriteLine("=== Per Core Usage ===");
//        for (var i = 0; i < cpuLoad.UsagePerCore.Length; i++)
//        {
//            Console.WriteLine($"  Core {i,2}: {cpuLoad.UsagePerCore[i]:P1}");
//        }

//        return ValueTask.CompletedTask;
//    }
//}

//--------------------------------------------------------------------------------
// LoadAverage
//--------------------------------------------------------------------------------
[Command("load", "Get load average")]
public sealed class LoadCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var load = PlatformProvider.GetLoadAverage();
        Console.WriteLine($"Average1:  {load.Average1:F2}");
        Console.WriteLine($"Average5:  {load.Average5:F2}");
        Console.WriteLine($"Average15: {load.Average15:F2}");

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
        var usage = mem.PhysicalMemory > 0 ? (double)mem.UsedBytes / mem.PhysicalMemory * 100 : 0;
        Console.WriteLine($"PhysicalMemory:   {DisplayFormatter.FormatBytes(mem.PhysicalMemory)}");
        Console.WriteLine($"Used:             {DisplayFormatter.FormatBytes(mem.UsedBytes)}  ({usage:F1}%)");
        Console.WriteLine($"Free:             {DisplayFormatter.FormatBytes(mem.FreeBytes)}");
        Console.WriteLine($"Active:           {DisplayFormatter.FormatBytes(mem.ActiveBytes)}");
        Console.WriteLine($"Inactive:         {DisplayFormatter.FormatBytes(mem.InactiveBytes)}");
        Console.WriteLine($"Wired:            {DisplayFormatter.FormatBytes(mem.WiredBytes)}");
        Console.WriteLine($"AppMemory:        {DisplayFormatter.FormatBytes(mem.AppMemoryBytes)}");
        Console.WriteLine($"Compressed:       {DisplayFormatter.FormatBytes(mem.CompressorBytes)}");
        var compressionRatio = mem.CompressorPageCount > 0 ? (double)mem.TotalUncompressedPagesInCompressor / mem.CompressorPageCount : 0;
        Console.WriteLine($"CompressionRatio: {compressionRatio:F2}");

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
        Console.WriteLine($"Used:      {DisplayFormatter.FormatBytes(swap.UsedBytes)}  ({usage:F1}%)");
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
            Console.WriteLine($"DiskSize:        {d.DiskSize}");
            Console.WriteLine($"BytesRead:       {d.BytesRead}");
            Console.WriteLine($"BytesWrite:      {d.BytesWrite}");
            Console.WriteLine($"ReadsCompleted:  {d.ReadsCompleted}");
            Console.WriteLine($"WritesCompleted: {d.WritesCompleted}");
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
            Console.WriteLine($"MountPoint:    {fs.MountPoint}");
            Console.WriteLine($"DeviceName:    {fs.DeviceName}");
            Console.WriteLine($"FileSystem:    {fs.FileSystem}");
            Console.WriteLine($"Option:        {fs.Option}");
            Console.WriteLine($"BlockSize:     {fs.BlockSize}");
            Console.WriteLine($"IOSize:        {fs.IOSize}");
            Console.WriteLine($"TotalSize:     {DisplayFormatter.FormatBytes(fs.TotalSize)}");
            Console.WriteLine($"FreeSize:      {DisplayFormatter.FormatBytes(fs.FreeSize)}");
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
            Console.WriteLine($"RxBytes:      {nif.RxBytes}");
            Console.WriteLine($"RxPackets:    {nif.RxPackets}");
            Console.WriteLine($"RxErrors:     {nif.RxErrors}");
            Console.WriteLine($"RxDrops:      {nif.RxDrops}");
            Console.WriteLine($"RxMulticast:  {nif.RxMulticast}");
            Console.WriteLine($"TxBytes:      {nif.TxBytes}");
            Console.WriteLine($"TxPackets:    {nif.TxPackets}");
            Console.WriteLine($"TxErrors:     {nif.TxErrors}");
            Console.WriteLine($"TxMulticast:  {nif.TxMulticast}");
            Console.WriteLine($"Collisions:   {nif.Collisions}");
            Console.WriteLine($"NoProto:      {nif.NoProto}");
            Console.WriteLine();
        }

        return ValueTask.CompletedTask;
    }
}

// TODO CPU?

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
            Console.WriteLine($"DeviceUtilization:   {device.DeviceUtilization}%");
            Console.WriteLine($"RendererUtilization: {device.RendererUtilization}%");
            Console.WriteLine($"TilerUtilization:    {device.TilerUtilization}%");
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
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var power = PlatformProvider.GetPowerStat();
        if (!power.Supported)
        {
            Console.WriteLine("Apple Silicon power reporting not supported (requires ARM64).");
            return;
        }

        // 1st snapshot (baseline)
        power.Update();
        var prevCpu = power.Cpu;
        var prevGpu = power.Gpu;
        var prevAne = power.Ane;
        var prevRam = power.Ram;
        var prevPci = power.Pci;
        var prevTotal = power.Total;
        var prevTime = DateTime.UtcNow;

        Console.WriteLine("Sampling (1000ms)...");

        await Task.Delay(1000);

        // 2nd snapshot
        power.Update();
        var elapsed = (DateTime.UtcNow - prevTime).TotalSeconds;

        Console.WriteLine("[Cumulative Energy]");
        Console.WriteLine($"  CPU Energy: {power.Cpu:F6} J");
        Console.WriteLine($"  GPU Energy: {power.Gpu:F6} J");
        Console.WriteLine($"  ANE Energy: {power.Ane:F6} J");
        Console.WriteLine($"  RAM Energy: {power.Ram:F6} J");
        Console.WriteLine($"  PCI Energy: {power.Pci:F6} J");
        Console.WriteLine($"  Total:      {power.Total:F6} J");
        Console.WriteLine();
        Console.WriteLine($"[Instantaneous Power (over {elapsed:F2}s)]");
        Console.WriteLine($"  CPU Power:  {(power.Cpu - prevCpu) / elapsed:F2} W");
        Console.WriteLine($"  GPU Power:  {(power.Gpu - prevGpu) / elapsed:F2} W");
        Console.WriteLine($"  ANE Power:  {(power.Ane - prevAne) / elapsed:F2} W");
        Console.WriteLine($"  RAM Power:  {(power.Ram - prevRam) / elapsed:F2} W");
        Console.WriteLine($"  PCI Power:  {(power.Pci - prevPci) / elapsed:F2} W");
        Console.WriteLine($"  Total:      {(power.Total - prevTotal) / elapsed:F2} W");
    }
}

// TODO
//// Temperature
//[Command("temp", "Temperature Sensors")]
//public sealed class TemperatureCommand : ICommandHandler
//{
//    public ValueTask ExecuteAsync(CommandContext context)
//    {
//        var temps = PlatformProvider.GetTemperatureSensors();
//        Console.WriteLine("=== Temperature Sensors ===");
//        if (temps.Count > 0)
//        {
//            Console.WriteLine($"{"Key",-6} {"Value",8}  Description");
//            Console.WriteLine(new string('-', 50));
//            foreach (var s in temps.Take(30))
//            {
//                Console.WriteLine($"{s.Key,-6} {s.Value,7:F1} C  {s.Description}");
//            }

//            if (temps.Count > 30)
//            {
//                Console.WriteLine($"... and {temps.Count - 30} more sensors");
//            }
//        }
//        else
//        {
//            Console.WriteLine("No temperature sensors found.");
//        }

//        return ValueTask.CompletedTask;
//    }
//}

//// Fan
//[Command("fan", "Fan Info")]
//public sealed class FanCommand : ICommandHandler
//{
//    public ValueTask ExecuteAsync(CommandContext context)
//    {
//        var fans = PlatformProvider.GetFans();
//        Console.WriteLine("=== Fan Info ===");
//        if (fans.Count > 0)
//        {
//            Console.WriteLine($"Fan count: {fans.Count}");
//            foreach (var fan in fans)
//            {
//                Console.WriteLine($"  Fan {fan.Index}:");
//                Console.WriteLine($"    Actual:  {fan.ActualRpm:F0} RPM");
//                Console.WriteLine($"    Min:     {fan.MinRpm:F0} RPM");
//                Console.WriteLine($"    Max:     {fan.MaxRpm:F0} RPM");
//                Console.WriteLine($"    Target:  {fan.TargetRpm:F0} RPM");
//            }
//        }
//        else
//        {
//            Console.WriteLine("No fans found.");
//        }

//        return ValueTask.CompletedTask;
//    }
//}

//// Power
//[Command("power", "Power Readings")]
//public sealed class PowerCommand : ICommandHandler
//{
//    public ValueTask ExecuteAsync(CommandContext context)
//    {
//        var powers = PlatformProvider.GetPowerReadings();
//        Console.WriteLine("=== Power Readings ===");
//        if (powers.Count > 0)
//        {
//            Console.WriteLine($"{"Key",-6} {"Value",8}  Description");
//            Console.WriteLine(new string('-', 50));
//            foreach (var p in powers)
//            {
//                Console.WriteLine($"{p.Key,-6} {p.Value,7:F2} W  {p.Description}");
//            }
//        }
//        else
//        {
//            Console.WriteLine("No power readings found.");
//        }

//        return ValueTask.CompletedTask;
//    }
//}

//// Voltage
//[Command("voltage", "Voltage Readings")]
//public sealed class VoltageCommand : ICommandHandler
//{
//    public ValueTask ExecuteAsync(CommandContext context)
//    {
//        var voltages = PlatformProvider.GetVoltageReadings();
//        Console.WriteLine("=== Voltage Readings ===");
//        if (voltages.Count > 0)
//        {
//            Console.WriteLine($"{"Key",-6} {"Value",8}  Description");
//            Console.WriteLine(new string('-', 50));
//            foreach (var v in voltages)
//            {
//                Console.WriteLine($"{v.Key,-6} {v.Value,7:F3} V  {v.Description}");
//            }
//        }
//        else
//        {
//            Console.WriteLine("No voltage readings found.");
//        }

//        return ValueTask.CompletedTask;
//    }
//}
