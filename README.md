# macOS platform library for .NET

|Library|NuGet|
|:----|:----|
|MacDotNet.SystemInfo|[![NuGet](https://img.shields.io/nuget/v/MacDotNet.SystemInfo.svg)](https://www.nuget.org/packages/MacDotNet.SystemInfo)|
|MacDotNet.Disk|[![NuGet](https://img.shields.io/nuget/v/MacDotNet.Disk.svg)](https://www.nuget.org/packages/MacDotNet.Disk)|

---

# 🖥️ MacDotNet.SystemInfo

System information api.

## Output sample

```
CPU Usage:                   Total: 6.0 %  (E: 8.0 %  P: 4.6 %)
CPU Usage Breakdown:         User: 2.5 %  System: 3.4 %  Idle: 94.0 %
CPU Frequency All:           1849 MHz  (E: 1494 MHz  P: 2086 MHz)
Uptime:                      0.15:20:40
System:                      Processes: 487  Threads: 2223
Handles:                     Files: 1234  Vnodes: 5678
Load Average:                1.24  1.37  1.39  (1/5/15 min)
Memory Usage:                57.5 %  (Active: 38.5 %  Wired: 11.6 %  Compressor: 7.4 %)
Swap Usage:                  0.0 %
GPU [AGXAcceleratorG14X]:    Device: 0 %  Renderer: 0 %  Tiler: 0 %
Disk disk0 (AppleFabric):    Read: 14.6 KB/s  Write: 0.0 KB/s
Disk disk6 (Usb):            Read: 0.0 KB/s  Write: 0.0 KB/s
FS / (apfs):                 48.1 %  (460 GB total)
FS /Volumes/Storage (apfs):  24.6 %  (931 GB total)
Net en0 (Ethernet):          DL: 1.0 KB/s  UL: 0.0 KB/s  Total RX: 294 MB  TX: 60 MB
Net en1 (Wi-Fi):             DL: 0.0 KB/s  UL: 0.0 KB/s  Total RX: 14 MB  TX: 7 MB
Temp CPU:                    41.02 C
Temp Mainboard:              30.03 C
Temp NAND:                   27.11 C
Temp SSD:                    33.05 C
Voltage DC-in:               12.560 V
Current DC-in:               0.617 A
Power DC-in:                 7.78 W
Power Total System:          7.74 W
Fan 0:                       1703 rpm  (34.1 %)  [min: 1700  max: 5000]
Power:                       CPU: 0.61 W  GPU: 0.00 W  ANE: 0.00 W  RAM: 0.09 W  PCI: 0.00 W
```

## Usage

### Hardware

```csharp
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
Console.WriteLine($"  MemorySize:        {hw.MemorySize / 1024 / 1024 / 1024} GB");
Console.WriteLine($"  PageSize:          {hw.PageSize} bytes");

Console.WriteLine("[Cache]");
Console.WriteLine($"  CacheLineSize:     {hw.CacheLineSize} bytes");
Console.WriteLine($"  L1I:               {hw.L1ICacheSize / 1024} KB");
Console.WriteLine($"  L1D:               {hw.L1DCacheSize / 1024} KB");
Console.WriteLine($"  L2:                {hw.L2CacheSize / 1024} KB");
Console.WriteLine($"  L3:                {hw.L3CacheSize / 1024} KB");

if (hw.PerformanceCoreCount > 0)
{
    Console.WriteLine("[CPU Cores]");
    var pCore = hw.PerformanceCoreLevel;
    Console.WriteLine($"  P-Core ({pCore.Name}): {pCore.PhysicalCpu} physical, {pCore.LogicalCpu} logical");
    if (hw.EfficiencyCoreCount > 0)
    {
        var eCore = hw.EfficiencyCoreLevel;
        Console.WriteLine($"  E-Core ({eCore.Name}): {eCore.PhysicalCpu} physical, {eCore.LogicalCpu} logical");
    }
}

if (hw.Gpus.Count > 0)
{
    Console.WriteLine("[GPU]");
    foreach (var gpu in hw.Gpus)
    {
        Console.WriteLine($"  Model:       {gpu.Model}");
        Console.WriteLine($"  Name:        {gpu.Name}");
        Console.WriteLine($"  CoreCount:   {gpu.CoreCount}");
        Console.WriteLine($"  VendorId:    0x{gpu.VendorId:X}");
        Console.WriteLine($"  MetalPlugin: {gpu.MetalPluginName}");
    }
}
```

### Kernel

```csharp
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
Console.WriteLine($"MaxVnodes:           {kernel.MaxVnodes}");
Console.WriteLine($"MaxSockets:          {kernel.MaxSockets}");
Console.WriteLine($"MaxArguments:        {kernel.MaxArguments}");
Console.WriteLine($"SecureLevel:         {kernel.SecureLevel}");
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
var usage = mem.PhysicalMemory > 0 ? (double)mem.UsedBytes / mem.PhysicalMemory * 100 : 0;

Console.WriteLine("[Usage]");
Console.WriteLine($"  Total:       {mem.PhysicalMemory / 1024 / 1024} MB");
Console.WriteLine($"  Used:        {mem.UsedBytes / 1024 / 1024} MB  ({usage:F1}%)");
Console.WriteLine($"  Free:        {mem.FreeBytes / 1024 / 1024} MB");

Console.WriteLine("[Breakdown]");
Console.WriteLine($"  Active:      {mem.ActiveBytes / 1024 / 1024} MB  ({mem.ActiveCount} pages)");
Console.WriteLine($"  Inactive:    {mem.InactiveBytes / 1024 / 1024} MB  ({mem.InactiveCount} pages)");
Console.WriteLine($"  Wired:       {mem.WiredBytes / 1024 / 1024} MB  ({mem.WireCount} pages)");
Console.WriteLine($"  AppMemory:   {mem.AppMemoryBytes / 1024 / 1024} MB");
Console.WriteLine($"  Compressed:  {mem.CompressorBytes / 1024 / 1024} MB  ({mem.CompressorPageCount} pages)");

Console.WriteLine("[Compressor]");
Console.WriteLine($"  Compressions:  {mem.Compression}");
Console.WriteLine($"  Decompression: {mem.Decompression}");
```

### Swap

```csharp
var swap = PlatformProvider.GetSwapUsage();
var usage = swap.TotalBytes > 0 ? (double)swap.UsedBytes / swap.TotalBytes * 100 : 0;
Console.WriteLine($"Total:     {swap.TotalBytes / 1024 / 1024} MB");
Console.WriteLine($"Used:      {swap.UsedBytes / 1024 / 1024} MB  ({usage:F1}%)");
Console.WriteLine($"Available: {swap.AvailableBytes / 1024 / 1024} MB");
Console.WriteLine($"Encrypted: {swap.IsEncrypted}");
```

### Disk Stat

```csharp
var diskStat = PlatformProvider.GetDiskStat();
foreach (var d in diskStat.Devices.Where(static d => d.IsPhysical))
{
    var label = d.MediaName is not null ? $"{d.BsdName} [{d.MediaName}]" : d.BsdName;
    Console.WriteLine($"[Device] {label}");
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
    Console.WriteLine($"  DiskSize:        {d.DiskSize / 1024 / 1024 / 1024} GB");
    Console.WriteLine($"  BytesRead:       {d.BytesRead / 1024 / 1024} MB");
    Console.WriteLine($"  BytesWrite:      {d.BytesWrite / 1024 / 1024} MB");
    Console.WriteLine($"  ReadsCompleted:  {d.ReadsCompleted}");
    Console.WriteLine($"  WritesCompleted: {d.WritesCompleted}");
    Console.WriteLine($"  TimeRead:        {d.TotalTimeRead / 1_000_000} ms");
    Console.WriteLine($"  TimeWrite:       {d.TotalTimeWrite / 1_000_000} ms");
    Console.WriteLine($"  ErrorsRead:      {d.ErrorsRead}");
    Console.WriteLine($"  ErrorsWrite:     {d.ErrorsWrite}");
}
```

### File System

```csharp
var fsStat = PlatformProvider.GetFileSystemStat();
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
    Console.WriteLine($"  Usage:         {usage:F1}%  ({usedSize / 1024 / 1024 / 1024} GB / {fs.TotalSize / 1024 / 1024 / 1024} GB)");
    Console.WriteLine($"  AvailableSize: {fs.AvailableSize / 1024 / 1024 / 1024} GB");
    Console.WriteLine($"  TotalFiles:    {fs.TotalFiles}");
    Console.WriteLine($"  FreeFiles:     {fs.FreeFiles}");
}
```

### Network Stat

```csharp
var network = PlatformProvider.GetNetworkStat();
foreach (var nif in network.Interfaces.Where(static x => x.IsEnabled))
{
    var label = nif.DisplayName is not null ? $" {nif.DisplayName}" : string.Empty;
    Console.WriteLine($"[{nif.Name}]{label} ({nif.InterfaceType})");
    Console.WriteLine($"  RxBytes:   {nif.RxBytes / 1024 / 1024} MB");
    Console.WriteLine($"  RxPackets: {nif.RxPackets}");
    Console.WriteLine($"  RxErrors:  {nif.RxErrors}");
    Console.WriteLine($"  TxBytes:   {nif.TxBytes / 1024 / 1024} MB");
    Console.WriteLine($"  TxPackets: {nif.TxPackets}");
    Console.WriteLine($"  TxErrors:  {nif.TxErrors}");
}
```

### Process

```csharp
var summary = PlatformProvider.GetProcessSummary();
Console.WriteLine($"Process Count:   {summary.ProcessCount}");
Console.WriteLine($"Thread Count:    {summary.ThreadCount}");
Console.WriteLine($"Open File Count: {summary.OpenFileCount}");

var processes = PlatformProvider.GetProcesses();
foreach (var p in processes.OrderBy(static p => p.ProcessId))
{
    var rss = (double)p.ResidentMemorySize / 1024 / 1024;
    var cpu = (p.UserTime + p.SystemTime).TotalSeconds;
    Console.WriteLine($"{p.ProcessId,-6} {p.Name,-20} {p.Status,-12} {p.UserId,-5} Threads={p.ThreadCount,3}  RSS={rss,8:F2} MB  CPU={cpu,8:F2}s");
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
    Console.WriteLine("Power reporting not supported.");
    return;
}

power.Update();
var prevCpu = power.Cpu;
var prevGpu = power.Gpu;
var prevAne = power.Ane;
var prevRam = power.Ram;
var prevPci = power.Pci;
var prevTotal = power.Total;

await Task.Delay(1000);
power.Update();

Console.WriteLine($"CPU:   {power.Cpu - prevCpu:F2} W");
Console.WriteLine($"GPU:   {power.Gpu - prevGpu:F2} W");
Console.WriteLine($"ANE:   {power.Ane - prevAne:F2} W");
Console.WriteLine($"RAM:   {power.Ram - prevRam:F2} W");
Console.WriteLine($"PCI:   {power.Pci - prevPci:F2} W");
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

SMART information.

## Usage

```csharp
var disks = DiskInfo.GetInformation();
foreach (var disk in disks)
{
    using (disk)
    {
        Console.WriteLine($"[disk{disk.Index}] {disk.Model}");
        Console.WriteLine($"  BsdName:           {disk.BsdName}");
        Console.WriteLine($"  SerialNumber:      {disk.SerialNumber}");
        Console.WriteLine($"  FirmwareRevision:  {disk.FirmwareRevision}");
        Console.WriteLine($"  Size:              {disk.Size / 1024 / 1024 / 1024} GB");
        Console.WriteLine($"  MediumType:        {disk.MediumType}");
        Console.WriteLine($"  BusType:           {disk.BusType}");
        Console.WriteLine($"  BusLocation:       {disk.BusLocation}");
        Console.WriteLine($"  ContentType:       {disk.ContentType}");
        Console.WriteLine($"  SmartType:         {disk.SmartType}");

        disk.Smart.Update();
        if (disk.SmartType == SmartType.Nvme)
        {
            PrintNvmeSmart((ISmartNvme)disk.Smart);
        }
        else if (disk.SmartType == SmartType.Generic)
        {
            PrintGenericSmart((ISmartGeneric)disk.Smart);
        }
    }
}

static void PrintNvmeSmart(ISmartNvme smart)
{
    Console.WriteLine($"  SMART (NVMe): LastUpdate=[{smart.LastUpdate}]");
    Console.WriteLine($"    Temperature:     {smart.Temperature} C");
    Console.WriteLine($"    AvailableSpare:  {smart.AvailableSpare} %");
    Console.WriteLine($"    PercentageUsed:  {smart.PercentageUsed} %");
    Console.WriteLine($"    DataUnitRead:    {smart.DataUnitRead}");
    Console.WriteLine($"    DataUnitWritten: {smart.DataUnitWritten}");
    Console.WriteLine($"    PowerCycles:     {smart.PowerCycles}");
    Console.WriteLine($"    PowerOnHours:    {smart.PowerOnHours}");
    Console.WriteLine($"    UnsafeShutdowns: {smart.UnsafeShutdowns}");
    Console.WriteLine($"    MediaErrors:     {smart.MediaErrors}");
    Console.WriteLine($"    CriticalWarning: {smart.CriticalWarning}");
}

static void PrintGenericSmart(ISmartGeneric smart)
{
    Console.WriteLine($"  SMART (Generic): LastUpdate=[{smart.LastUpdate}]");
    Console.WriteLine("    ID   FLAG   CUR  WOR  RAW");
    Console.WriteLine("    ---  ----   ---  ---  --------");
    foreach (var id in smart.GetSupportedIds())
    {
        var attr = smart.GetAttribute(id);
        if (attr.HasValue)
        {
            Console.WriteLine($"    {(byte)id,3}  0x{attr.Value.Flags:X4}  {attr.Value.CurrentValue,3}  {attr.Value.WorstValue,3}  {attr.Value.RawValue}");
        }
    }
}
```

# 🌐Link

- [LinuxDotNet](https://github.com/usausa/linux-dotnet)
- [RaspberryDotNet](https://github.com/usausa/raspberrypi-dotnet)
- [Disk information library](https://github.com/usausa/hardwareinfo-disk)
