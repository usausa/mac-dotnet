# macOS platform library for .NET

|Library|NuGet|
|:----|:----|
|MacDotNet.SystemInfo|[![NuGet](https://img.shields.io/nuget/v/MacDotNet.SystemInfo.svg)](https://www.nuget.org/packages/MacDotNet.SystemInfo)|
|MacDotNet.Disk|[![NuGet](https://img.shields.io/nuget/v/MacDotNet.Disk.svg)](https://www.nuget.org/packages/MacDotNet.Disk)|

---

# 🖥️ MacDotNet.SystemInfo

System information api.

## Usage

### Hardware

```csharp
var hw = PlatformProvider.GetHardware();

Console.WriteLine("[System]");
Console.WriteLine($"Model:             {hw.Model}");
Console.WriteLine($"Machine:           {hw.Machine}");
Console.WriteLine($"SerialNumber:      {hw.SerialNumber}");

Console.WriteLine("[CPU]");
Console.WriteLine($"CpuBrand:          {hw.CpuBrandString}");
Console.WriteLine($"PhysicalCpu:       {hw.PhysicalCpu} (max: {hw.PhysicalCpuMax})");
Console.WriteLine($"LogicalCpu:        {hw.LogicalCpu} (max: {hw.LogicalCpuMax})");
Console.WriteLine($"ActiveCpu:         {hw.ActiveCpu}");
Console.WriteLine($"CoreCount:         {hw.CpuCoreCount}");
Console.WriteLine($"ThreadCount:       {hw.CpuThreadCount}");
Console.WriteLine($"TimebaseFrequency: {hw.TimebaseFrequency} Hz");

Console.WriteLine("[Memory]");
Console.WriteLine($"MemorySize:        {hw.MemorySize / 1024 / 1024 / 1024} GB");
Console.WriteLine($"PageSize:          {hw.PageSize} bytes");

Console.WriteLine("[Cache]");
Console.WriteLine($"CacheLineSize:     {hw.CacheLineSize} bytes");
Console.WriteLine($"L1I:               {hw.L1ICacheSize / 1024} KB");
Console.WriteLine($"L1D:               {hw.L1DCacheSize / 1024} KB");
Console.WriteLine($"L2:                {hw.L2CacheSize / 1024} KB");

// P-core / E-core
if (hw.PerformanceCoreCount > 0)
{
    var pCore = hw.PerformanceCoreLevel;
    Console.WriteLine($"P-Core ({pCore.Name}): {pCore.PhysicalCpu} physical, {pCore.LogicalCpu} logical");
    var eCore = hw.EfficiencyCoreLevel;
    Console.WriteLine($"E-Core ({eCore.Name}): {eCore.PhysicalCpu} physical, {eCore.LogicalCpu} logical");
}

// GPU list
foreach (var gpu in hw.Gpus)
{
    Console.WriteLine($"GPU Model:   {gpu.Model}");
    Console.WriteLine($"GPU Cores:   {gpu.CoreCount}");
}
```

### Kernel

```csharp
var kernel = PlatformProvider.GetKernel();
Console.WriteLine($"OsType:              {kernel.OsType}");
Console.WriteLine($"OsRelease:           {kernel.OsRelease}");
Console.WriteLine($"OsVersion:           {kernel.OsVersion}");
Console.WriteLine($"OsProductVersion:    {kernel.OsProductVersion}");
Console.WriteLine($"KernelVersion:       {kernel.KernelVersion}");
Console.WriteLine($"Uuid:                {kernel.Uuid}");
Console.WriteLine($"MaxProcesses:        {kernel.MaxProcesses}");
Console.WriteLine($"MaxFiles:            {kernel.MaxFiles}");
Console.WriteLine($"BootTime:            {kernel.BootTime:yyyy-MM-dd HH:mm:ss zzz}");
```

### Uptime

```csharp
var uptime = PlatformProvider.GetUptime();
Console.WriteLine($"Uptime: {(int)uptime.Elapsed.TotalDays}d {uptime.Elapsed.Hours:D2}:{uptime.Elapsed.Minutes:D2}:{uptime.Elapsed.Seconds:D2}");
```

### LoadAverage

```csharp
var load = PlatformProvider.GetLoadAverage();
Console.WriteLine($"Average1:  {load.Average1:F2}");
Console.WriteLine($"Average5:  {load.Average5:F2}");
Console.WriteLine($"Average15: {load.Average15:F2}");
```

### CPU Stat

```csharp
var stat = PlatformProvider.GetCpuStat();
// Cores grouped by type
foreach (var core in stat.PerformanceCores)
{
    Console.WriteLine($"P-Core {core.Number}: User={core.User} System={core.System} Idle={core.Idle}");
}
foreach (var core in stat.EfficiencyCores)
{
    Console.WriteLine($"E-Core {core.Number}: User={core.User} System={core.System} Idle={core.Idle}");
}
```

### CPU Frequency

```csharp
var cpuFreq = PlatformProvider.GetCpuFrequency();
Console.WriteLine($"Max E-Core: {cpuFreq.MaxEfficiencyCoreFrequency} MHz");
Console.WriteLine($"Max P-Core: {cpuFreq.MaxPerformanceCoreFrequency} MHz");

foreach (var core in cpuFreq.EfficiencyCores)
{
    Console.WriteLine($"E-Core {core.Number}: {core.Frequency:F1} MHz");
}
foreach (var core in cpuFreq.PerformanceCores)
{
    Console.WriteLine($"P-Core {core.Number}: {core.Frequency:F1} MHz");
}
```

### Memory

```csharp
var mem = PlatformProvider.GetMemoryStat();
Console.WriteLine($"PhysicalMemory: {mem.PhysicalMemory / 1024 / 1024} MB");
Console.WriteLine($"Active:         {mem.ActiveCount} pages");
Console.WriteLine($"Inactive:       {mem.InactiveCount} pages");
Console.WriteLine($"Wired:          {mem.WireCount} pages");
Console.WriteLine($"Free:           {mem.FreeCount} pages");
Console.WriteLine($"PageIn:         {mem.PageIn}");
Console.WriteLine($"PageOut:        {mem.PageOut}");
Console.WriteLine($"SwapIn:         {mem.SwapIn}");
Console.WriteLine($"SwapOut:        {mem.SwapOut}");
```

### Swap

```csharp
var swap = PlatformProvider.GetSwapUsage();
Console.WriteLine($"Total:     {swap.TotalBytes / 1024 / 1024} MB");
Console.WriteLine($"Used:      {swap.UsedBytes / 1024 / 1024} MB");
Console.WriteLine($"Available: {swap.AvailableBytes / 1024 / 1024} MB");
Console.WriteLine($"Encrypted: {swap.IsEncrypted}");
```

### Disk Stat

```csharp
var diskStat = PlatformProvider.GetDiskStat();
foreach (var d in diskStat.Devices.Where(d => d.IsPhysical))
{
    Console.WriteLine($"[{d.Name}]  BusType: {d.BusType}  Size: {d.DiskSize / 1024 / 1024 / 1024} GB");
    Console.WriteLine($"  BytesRead:       {d.BytesRead}");
    Console.WriteLine($"  BytesWrite:      {d.BytesWrite}");
    Console.WriteLine($"  ReadsCompleted:  {d.ReadsCompleted}");
    Console.WriteLine($"  WritesCompleted: {d.WritesCompleted}");
    Console.WriteLine($"  ErrorsRead:      {d.ErrorsRead}");
    Console.WriteLine($"  ErrorsWrite:     {d.ErrorsWrite}");
}
```

### File System

```csharp
var fileSystems = PlatformProvider.GetFileSystems();
foreach (var fs in fileSystems)
{
    Console.WriteLine($"MountPoint:    {fs.MountPoint}");
    Console.WriteLine($"DeviceName:    {fs.DeviceName}");
    Console.WriteLine($"FileSystem:    {fs.FileSystem}");
    Console.WriteLine($"TotalSize:     {fs.TotalSize / 1024 / 1024 / 1024} GB");
    Console.WriteLine($"AvailableSize: {fs.AvailableSize / 1024 / 1024 / 1024} GB");
    Console.WriteLine($"TotalFiles:    {fs.TotalFiles}");
}

var usage = PlatformProvider.GetFileSystemUsage("/");
Console.WriteLine($"TotalSize:     {usage.TotalSize}");
Console.WriteLine($"FreeSize:      {usage.FreeSize}");
Console.WriteLine($"AvailableSize: {usage.AvailableSize}");
```

### Network Stat

```csharp
var network = PlatformProvider.GetNetworkStat();
foreach (var nif in network.Interfaces.Where(x => x.IsEnabled))
{
    Console.WriteLine($"[{nif.Name}] {nif.DisplayName} ({nif.InterfaceType})");
    Console.WriteLine($"  RxBytes:   {nif.RxBytes}");
    Console.WriteLine($"  RxPackets: {nif.RxPackets}");
    Console.WriteLine($"  RxErrors:  {nif.RxErrors}");
    Console.WriteLine($"  TxBytes:   {nif.TxBytes}");
    Console.WriteLine($"  TxPackets: {nif.TxPackets}");
    Console.WriteLine($"  TxErrors:  {nif.TxErrors}");
}
```

### Process

```csharp
var summary = PlatformProvider.GetProcessSummary();
Console.WriteLine($"ProcessCount: {summary.ProcessCount}");
Console.WriteLine($"ThreadCount:  {summary.ThreadCount}");

var processes = PlatformProvider.GetProcesses();
foreach (var p in processes.OrderBy(p => p.ProcessId))
{
    Console.WriteLine($"PID={p.ProcessId,-6} Name={p.Name,-20} Status={p.Status}");
    Console.WriteLine($"  Threads={p.ThreadCount}  RSS={p.ResidentMemorySize / 1024 / 1024} MB");
    Console.WriteLine($"  UserTime={p.UserTime.TotalSeconds:F2}s  SystemTime={p.SystemTime.TotalSeconds:F2}s");
}

var proc = PlatformProvider.GetProcess(Environment.ProcessId);
if (proc is not null)
{
    Console.WriteLine($"Self: {proc.Name} ({proc.Status})");
}
```

### GPU Devices

```csharp
var devices = PlatformProvider.GetGpuDevices();
foreach (var device in devices)
{
    Console.WriteLine($"[{device.Name}]");
    Console.WriteLine($"  DeviceUtilization:   {device.DeviceUtilization}%");
    Console.WriteLine($"  RendererUtilization: {device.RendererUtilization}%");
    Console.WriteLine($"  TilerUtilization:    {device.TilerUtilization}%");
    Console.WriteLine($"  AllocSystemMemory:   {device.AllocSystemMemory / 1024 / 1024} MB");
    Console.WriteLine($"  InUseSystemMemory:   {device.InUseSystemMemory / 1024 / 1024} MB");
    Console.WriteLine($"  Temperature:         {device.Temperature} C");
    Console.WriteLine($"  FanSpeed:            {device.FanSpeed}%");
    Console.WriteLine($"  CoreClock:           {device.CoreClock} MHz");
    Console.WriteLine($"  MemoryClock:         {device.MemoryClock} MHz");
    Console.WriteLine($"  PowerState:          {(device.PowerState ? "Active" : "Powered Off")}");
}
```

### Power Consumption

```csharp
var power = PlatformProvider.GetPowerStat();
if (!power.Supported)
{
    Console.WriteLine("Power reporting requires.");
    return;
}

// Measure wattage over 1 second
var prevCpu = power.Cpu;
var prevGpu = power.Gpu;
var prevAne = power.Ane;
var prevTotal = power.Total;

await Task.Delay(1000);
power.Update();

Console.WriteLine($"CPU: {power.Cpu - prevCpu:F2} W");
Console.WriteLine($"GPU: {power.Gpu - prevGpu:F2} W");
Console.WriteLine($"ANE: {power.Ane - prevAne:F2} W");
Console.WriteLine($"Total: {power.Total - prevTotal:F2} W");
```

### SMC Sensors

```csharp
var monitor = PlatformProvider.GetSmcMonitor();

// Temperature
foreach (var s in monitor.Temperatures)
{
    Console.WriteLine($"{s.Key} ({s.Description}): {s.Value:F1} C");
}
// Voltage
foreach (var s in monitor.Voltages)
{
    Console.WriteLine($"{s.Key} ({s.Description}): {s.Value:F3} V");
}
// Power
foreach (var s in monitor.Powers)
{
    Console.WriteLine($"{s.Key} ({s.Description}): {s.Value:F2} W");
}
// Current
foreach (var s in monitor.Currents)
{
    Console.WriteLine($"{s.Key} ({s.Description}): {s.Value:F3} A");
}
// Fan
foreach (var fan in monitor.Fans)
{
    Console.WriteLine($"Fan {fan.Index}: {fan.ActualRpm:F0} RPM (min={fan.MinRpm:F0}, max={fan.MaxRpm:F0})");
}
```

---

# 💽 MacDotNet.Disk

SMART infotmation.

## Usage

(TODO)
