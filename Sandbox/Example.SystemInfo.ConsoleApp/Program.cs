using MacDotNet.SystemInfo;

Console.WriteLine("=== MacDotNet.SystemInfo - Feature Demo ===");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 1. Kernel
// ---------------------------------------------------------------------------
Console.WriteLine("### 1. Kernel ###");
var kernel = PlatformProvider.GetKernel();
Console.WriteLine($"  OS Type:          {kernel.OsType}");
Console.WriteLine($"  OS Release:       {kernel.OsRelease}");
Console.WriteLine($"  OS Version:       {kernel.OsVersion}");
Console.WriteLine($"  OS Product Ver:   {kernel.OsProductVersion ?? "(unavailable)"}");
Console.WriteLine($"  Kernel Version:   {kernel.KernelVersion}");
Console.WriteLine($"  UUID:             {kernel.Uuid}");
Console.WriteLine($"  Boot Time:        {kernel.BootTime:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine($"  Uptime:           {DateTimeOffset.UtcNow - kernel.BootTime:d\\.hh\\:mm\\:ss}");
Console.WriteLine($"  Max Processes:    {kernel.MaxProc}");
Console.WriteLine($"  Max Files:        {kernel.MaxFiles}");
Console.WriteLine($"  Secure Level:     {kernel.SecureLevel}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 2. Hardware
// ---------------------------------------------------------------------------
Console.WriteLine("### 2. Hardware ###");
var hw = PlatformProvider.GetHardware();
Console.WriteLine($"  Model:            {hw.Model}");
Console.WriteLine($"  Machine:          {hw.Machine}");
Console.WriteLine($"  CPU Brand:        {hw.CpuBrandString ?? "(unavailable)"}");
Console.WriteLine($"  Physical CPU:     {hw.PhysicalCpu}");
Console.WriteLine($"  Logical CPU:      {hw.LogicalCpu}");
Console.WriteLine($"  Active CPU:       {hw.ActiveCpu}");
if (hw.CpuFrequency > 0)
{
    Console.WriteLine($"  CPU Frequency:    {hw.CpuFrequency / 1_000_000_000.0:F2} GHz");
}
Console.WriteLine($"  Memory:           {FormatBytes((ulong)hw.MemSize)}");
Console.WriteLine($"  Page Size:        {hw.PageSize} bytes");
if (hw.L2CacheSize > 0)
{
    Console.WriteLine($"  L2 Cache:         {FormatBytes((ulong)hw.L2CacheSize)}");
}
if (hw.L3CacheSize > 0)
{
    Console.WriteLine($"  L3 Cache:         {FormatBytes((ulong)hw.L3CacheSize)}");
}
Console.WriteLine();

// Apple Silicon パフォーマンスレベル
var perfLevels = PlatformProvider.GetPerformanceLevels();
if (perfLevels.Count > 0)
{
    Console.WriteLine("### 2a. Performance Levels (Apple Silicon) ###");
    foreach (var level in perfLevels)
    {
        var freqStr = level.CpuFrequencyMax > 0
            ? $", Freq={level.CpuFrequencyMax / 1_000_000_000.0:F2} GHz"
            : string.Empty;
        Console.WriteLine($"  [{level.Index}] {level.Name}: PhysicalCpu={level.PhysicalCpu}, LogicalCpu={level.LogicalCpu}, L2={FormatBytes((ulong)level.L2CacheSize)}{freqStr}");
    }
    Console.WriteLine();
}

// ---------------------------------------------------------------------------
// 3. CPU Stat  (1回目のスナップショットを保存し、500ms後に差分を計算して使用率を求める)
// ---------------------------------------------------------------------------
Console.WriteLine("### 3. CPU Stat ###");
Console.WriteLine("  Measuring (500ms)...");
var cpuStat = PlatformProvider.GetCpuStat();

// 1回目スナップショットを保存 (CpuCoreStat はミュータブルなため値をコピー)
var prevTotalUser = cpuStat.CpuTotal.User;
var prevTotalSystem = cpuStat.CpuTotal.System;
var prevTotalIdle = cpuStat.CpuTotal.Idle;
var prevTotalNice = cpuStat.CpuTotal.Nice;
var prevCores = cpuStat.CpuCores.Select(c => (c.User, c.System, c.Idle, c.Nice)).ToArray();

Thread.Sleep(500);
cpuStat.Update();

// 全体の使用率を計算
var dUser = cpuStat.CpuTotal.User - prevTotalUser;
var dSystem = cpuStat.CpuTotal.System - prevTotalSystem;
var dIdle = cpuStat.CpuTotal.Idle - prevTotalIdle;
var dNice = cpuStat.CpuTotal.Nice - prevTotalNice;
var dTotal = dUser + dSystem + dIdle + dNice;
var userLoad = dTotal > 0 ? (double)dUser / dTotal : 0;
var systemLoad = dTotal > 0 ? (double)dSystem / dTotal : 0;
var idleLoad = dTotal > 0 ? (double)dIdle / dTotal : 0;

Console.WriteLine($"  User:             {userLoad:P2}");
Console.WriteLine($"  System:           {systemLoad:P2}");
Console.WriteLine($"  Idle:             {idleLoad:P2}");
Console.WriteLine($"  Total:            {userLoad + systemLoad:P2}");

// Apple Silicon P-core / E-core の平均使用率
if (hw.PCoreCount > 0 && hw.ECoreCount > 0)
{
    var pLoad = 0.0;
    for (var i = 0; i < hw.PCoreCount && i < cpuStat.CpuCores.Count; i++)
    {
        var c = cpuStat.CpuCores[i];
        var p = prevCores[i];
        var dt = (c.User - p.User) + (c.System - p.System) + (c.Idle - p.Idle) + (c.Nice - p.Nice);
        pLoad += dt > 0 ? (double)((c.User - p.User) + (c.System - p.System) + (c.Nice - p.Nice)) / dt : 0;
    }
    Console.WriteLine($"  P-Core Average:   {pLoad / hw.PCoreCount:P2}");

    var eLoad = 0.0;
    for (var i = hw.PCoreCount; i < hw.PCoreCount + hw.ECoreCount && i < cpuStat.CpuCores.Count; i++)
    {
        var c = cpuStat.CpuCores[i];
        var p = prevCores[i];
        var dt = (c.User - p.User) + (c.System - p.System) + (c.Idle - p.Idle) + (c.Nice - p.Nice);
        eLoad += dt > 0 ? (double)((c.User - p.User) + (c.System - p.System) + (c.Nice - p.Nice)) / dt : 0;
    }
    Console.WriteLine($"  E-Core Average:   {eLoad / hw.ECoreCount:P2}");
}

// コアごとの使用率
Console.WriteLine($"  Per-Core ({cpuStat.CpuCores.Count} cores):");
for (var i = 0; i < Math.Min(cpuStat.CpuCores.Count, 12); i++)
{
    var c = cpuStat.CpuCores[i];
    var p = prevCores[i];
    var dt = (c.User - p.User) + (c.System - p.System) + (c.Idle - p.Idle) + (c.Nice - p.Nice);
    var usage = dt > 0 ? (double)((c.User - p.User) + (c.System - p.System) + (c.Nice - p.Nice)) / dt : 0;
    Console.WriteLine($"    Core {i,2}: {usage:P2}");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 4. Memory
// ---------------------------------------------------------------------------
Console.WriteLine("### 4. Memory ###");
var mem = PlatformProvider.GetMemoryStat();
Console.WriteLine($"  Physical Memory:  {FormatBytes(mem.PhysicalMemory)}");
Console.WriteLine($"  Used:             {FormatBytes(mem.UsedBytes)}  ({mem.UsagePercent:P1})");
Console.WriteLine($"  Free:             {FormatBytes(mem.FreeBytes)}");
Console.WriteLine($"  Active:           {FormatBytes(mem.ActiveBytes)}");
Console.WriteLine($"  Inactive:         {FormatBytes(mem.InactiveBytes)}");
Console.WriteLine($"  Wired:            {FormatBytes(mem.WiredBytes)}");
Console.WriteLine($"  App Memory:       {FormatBytes(mem.AppMemoryBytes)}");
Console.WriteLine($"  Compressed:       {FormatBytes(mem.CompressorBytes)}");
Console.WriteLine($"  Compression Ratio:{mem.CompressionRatio:F2}");
Console.WriteLine();

Console.WriteLine("### 4a. Swap ###");
var swap = PlatformProvider.GetSwapStat();
Console.WriteLine($"  Total:            {FormatBytes(swap.TotalBytes)}");
Console.WriteLine($"  Used:             {FormatBytes(swap.UsedBytes)}");
Console.WriteLine($"  Available:        {FormatBytes(swap.AvailableBytes)}");
Console.WriteLine($"  Encrypted:        {swap.IsEncrypted}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 5. File Systems
// ---------------------------------------------------------------------------
Console.WriteLine("### 5. File Systems ###");
var fileSystems = PlatformProvider.GetFileSystems();
foreach (var fs in fileSystems)
{
    Console.WriteLine($"  {fs.MountPoint} ({fs.TypeName})");
    Console.WriteLine($"    Device:    {fs.DeviceName}");
    Console.WriteLine($"    Total:     {FormatBytes(fs.TotalSize)}");
    Console.WriteLine($"    Available: {FormatBytes(fs.AvailableSize)}  ({fs.UsagePercent:P1} used)");
    Console.WriteLine($"    ReadOnly:  {fs.IsReadOnly}, Local: {fs.IsLocal}");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 5a. Disk Volumes (macOS ストレージ設定に表示されるユーザー可視ボリューム)
// Linux の GetPartitions() + GetFileSystemUsage() に相当する機能。
// "/" (Macintosh HD) および "/Volumes/*" のみを表示し、
// "/System/Volumes/*" 等のAPFS内部システムボリュームは除外する。
// ---------------------------------------------------------------------------
Console.WriteLine("### 5a. Disk Volumes ###");
var volumes = PlatformProvider.GetDiskVolumes();
foreach (var vol in volumes)
{
    var usage = PlatformProvider.GetFileSystemUsage(vol.MountPoint);
    Console.WriteLine($"  {vol.MountPoint} ({vol.TypeName})");
    Console.WriteLine($"    Device:    {vol.DeviceName}");
    Console.WriteLine($"    Total:     {FormatBytes(usage.TotalSize)}");
    Console.WriteLine($"    Used:      {FormatBytes(usage.TotalSize - usage.AvailableSize)}  ({usage.UsagePercent:P1} used)");
    Console.WriteLine($"    Available: {FormatBytes(usage.AvailableSize)}");
    Console.WriteLine($"    ReadOnly:  {vol.IsReadOnly}");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 6. Network Interfaces (macOS SC 固有情報)
// System.Net.NetworkInformation.NetworkInterface と Name (BSD 名) で突合して使用する。
// デフォルト: macOS System Settings で有効なサービスのみ。全取得は GetNetworkInterfaces(includeAll: true)
// ---------------------------------------------------------------------------
Console.WriteLine("### 6. Network Interfaces ###");
var interfaces = PlatformProvider.GetNetworkInterfaces();
// System.Net.NetworkInformation から補完情報を取得
var dotnetIfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
    .ToDictionary(ni => ni.Name, StringComparer.Ordinal);
foreach (var iface in interfaces)
{
    dotnetIfaces.TryGetValue(iface.Name, out var ni);
    var state = ni?.OperationalStatus.ToString() ?? "Unknown";
    Console.WriteLine($"  [{iface.Name}] {iface.DisplayName} ({iface.ScNetworkInterfaceType}) - {state}");
    if (ni is not null)
    {
        var mac = ni.GetPhysicalAddress().ToString();
        if (!string.IsNullOrEmpty(mac))
        {
            Console.WriteLine($"    MAC:    {string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2)))}");
        }
        foreach (var ua in ni.GetIPProperties().UnicastAddresses)
        {
            var family = ua.Address.AddressFamily;
            if (family == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                Console.WriteLine($"    IPv4:   {ua.Address}/{ua.PrefixLength}");
            }
            else if (family == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                Console.WriteLine($"    IPv6:   {ua.Address}/{ua.PrefixLength}");
            }
        }
    }
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 6a. Network Stats (トラフィック統計 / 500ms デルタ計測)
// System.Net の GetIPv4Statistics() に加え、デルタ値・Collisions 等 macOS 固有カウンタを提供
// ---------------------------------------------------------------------------
Console.WriteLine("### 6a. Network Stats (500ms delta) ###");
var netStats = PlatformProvider.GetNetworkStats();
// NetworkInterfaceStat はミュータブルなため値をコピー
var prevSnapshot = netStats.Interfaces.ToDictionary(
    x => x.Name,
    x => (x.RxBytes, x.RxPackets, x.RxErrors, x.TxBytes, x.TxPackets, x.TxErrors));
var netT0 = DateTime.UtcNow;
Thread.Sleep(500);
netStats.Update();
var netElapsed = (DateTime.UtcNow - netT0).TotalSeconds;
var serviceNames = interfaces.Select(i => i.Name).ToHashSet(StringComparer.Ordinal);
foreach (var s in netStats.Interfaces.Where(x => serviceNames.Contains(x.Name)))
{
    Console.WriteLine($"  [{s.Name}]");
    var hasPrev = prevSnapshot.TryGetValue(s.Name, out var prev);
    var deltaRxBytes   = hasPrev ? unchecked(s.RxBytes   - prev.RxBytes)   : 0u;
    var deltaRxPackets = hasPrev ? unchecked(s.RxPackets - prev.RxPackets) : 0u;
    var deltaRxErrors  = hasPrev ? unchecked(s.RxErrors  - prev.RxErrors)  : 0u;
    var deltaTxBytes   = hasPrev ? unchecked(s.TxBytes   - prev.TxBytes)   : 0u;
    var deltaTxPackets = hasPrev ? unchecked(s.TxPackets - prev.TxPackets) : 0u;
    var deltaTxErrors  = hasPrev ? unchecked(s.TxErrors  - prev.TxErrors)  : 0u;
    var rxKbps = netElapsed > 0 ? deltaRxBytes / 1024.0 / netElapsed : 0;
    var txKbps = netElapsed > 0 ? deltaTxBytes / 1024.0 / netElapsed : 0;
    Console.WriteLine($"    RX: {FormatBytes(s.RxBytes),10} total  {rxKbps,8:F2} KB/s  ({deltaRxPackets} pkts, {deltaRxErrors} err)");
    Console.WriteLine($"    TX: {FormatBytes(s.TxBytes),10} total  {txKbps,8:F2} KB/s  ({deltaTxPackets} pkts, {deltaTxErrors} err)");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 6b. Disk I/O Stats (500ms delta)
// 5a と同じ単位 (ユーザー可視ボリューム) で表示する。
// /dev/disk3s1s1 → 物理ディスク disk3 の I/O にマッピングする。
// ---------------------------------------------------------------------------
Console.WriteLine("### 6b. Disk I/O Stats (500ms delta) ###");
var diskStats = PlatformProvider.GetDiskStats();
// DiskDeviceStat はミュータブルなため値をコピー
var prevDiskSnapshot = diskStats.Devices.ToDictionary(
    x => x.Name,
    x => (x.BytesRead, x.BytesWritten));
var diskT0 = DateTime.UtcNow;
Thread.Sleep(500);
diskStats.Update();
var diskElapsed = (DateTime.UtcNow - diskT0).TotalSeconds;
var diskVolumes = PlatformProvider.GetDiskVolumes();
if (diskVolumes.Count == 0)
{
    Console.WriteLine("  No disk volumes found.");
}
foreach (var vol in diskVolumes)
{
    var physicalDisk = ExtractPhysicalDiskName(vol.DeviceName);
    var d = physicalDisk is not null
        ? diskStats.Devices.FirstOrDefault(x => x.Name == physicalDisk)
        : null;
    var diskLabel = physicalDisk is not null && d?.MediaName is not null
        ? $"{physicalDisk} [{d.MediaName}]"
        : physicalDisk ?? "?";
    Console.WriteLine($"  [{vol.MountPoint}] ({vol.DeviceName} → {diskLabel})");
    if (d is null)
    {
        Console.WriteLine($"    (I/O stats not available)");
        continue;
    }

    var hasPrevDisk = prevDiskSnapshot.TryGetValue(d.Name, out var prevDisk);
    var deltaRead    = hasPrevDisk ? d.BytesRead    - prevDisk.BytesRead    : 0UL;
    var deltaWritten = hasPrevDisk ? d.BytesWritten - prevDisk.BytesWritten : 0UL;
    var readMbps  = diskElapsed > 0 ? deltaRead    / (1024.0 * 1024.0) / diskElapsed : 0;
    var writeMbps = diskElapsed > 0 ? deltaWritten / (1024.0 * 1024.0) / diskElapsed : 0;
    Console.WriteLine($"    Read:  {FormatBytes(d.BytesRead),12} total  {readMbps,8:F2} MB/s");
    Console.WriteLine($"    Write: {FormatBytes(d.BytesWritten),12} total  {writeMbps,8:F2} MB/s");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 7. Processes (Top 10 by memory)
// ---------------------------------------------------------------------------
Console.WriteLine("### 7. Processes (Top 10 by RSS) ###");
var processes = PlatformProvider.GetProcesses();
Array.Sort(processes, static (a, b) => b.ResidentSize.CompareTo(a.ResidentSize));
var top10Count = Math.Min(10, processes.Length);
Console.WriteLine($"  {"PID",6} {"Name",-30} {"RSS",10} {"VSZ",12} {"Threads",8}");
for (var i = 0; i < top10Count; i++)
{
    var p = processes[i];
    Console.WriteLine($"  {p.ProcessId,6} {Truncate(p.Name, 30),-30} {FormatBytes(p.ResidentSize),10} {FormatBytes(p.VirtualSize),12} {p.ThreadCount,8}");
}
Console.WriteLine($"  Total processes: {processes.Length}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 8. GPU  (HardwareInfo.GetGpuInfos: 静的情報 / GpuDevice: 動的統計・センサー)
// ---------------------------------------------------------------------------
Console.WriteLine("### 8. GPU ###");
var gpuInfos = PlatformProvider.GetGpuInfos();
var gpuDevices = PlatformProvider.GetGpuDevices();
if (gpuDevices.Length == 0)
{
    Console.WriteLine("  No GPU found.");
}
foreach (var device in gpuDevices)
{
    var info = Array.Find(gpuInfos, g => g.ClassName == device.Name);
    Console.WriteLine($"  {info?.Model ?? device.Name}");
    Console.WriteLine($"    Class:       {device.Name}");
    if (info is not null)
    {
        Console.WriteLine($"    Cores:       {info.CoreCount}");
        Console.WriteLine($"    VendorId:    0x{info.VendorId:X4}");
        if (info.Configuration is not null)
        {
            Console.WriteLine($"    GPU Gen:     {info.Configuration.GpuGeneration}");
        }
    }
    Console.WriteLine($"    Utilization: {device.DeviceUtilization}%  (Renderer={device.RendererUtilization}%, Tiler={device.TilerUtilization}%)");
    Console.WriteLine($"    Mem Used:    {FormatBytes((ulong)device.InUseSystemMemory)}");
    if (device.Temperature is not null)
    {
        Console.WriteLine($"    Temperature: {device.Temperature}°C");
    }
    if (device.PowerState is not null)
    {
        Console.WriteLine($"    Power:       {(device.PowerState.Value ? "On" : "Off (AGC)")}");
    }
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 9. Battery
// ---------------------------------------------------------------------------
Console.WriteLine("### 9. Battery (IOPowerSources) ###");
var battery = PlatformProvider.GetBattery();
if (battery.IsPresent)
{
    Console.WriteLine($"  State:            {battery.PowerSourceState}");
    Console.WriteLine($"  Charging:         {battery.IsCharging}");
    Console.WriteLine($"  Charged:          {battery.IsCharged}");
    Console.WriteLine($"  Percent:          {battery.BatteryPercent}%");
    Console.WriteLine($"  Health:           {battery.BatteryHealth ?? "N/A"}");
    Console.WriteLine($"  Cycle Count:      {battery.DesignCycleCount}");
    if (battery.TimeToEmpty >= 0)
    {
        Console.WriteLine($"  Time to Empty:    {battery.TimeToEmpty} min");
    }
    if (battery.TimeToFullCharge >= 0)
    {
        Console.WriteLine($"  Time to Full:     {battery.TimeToFullCharge} min");
    }
}
else
{
    Console.WriteLine("  Battery not present.");
}
Console.WriteLine();

Console.WriteLine("### 9a. Battery Detail (IORegistry) ###");
var batteryDetail = PlatformProvider.GetBatteryDetail();
if (batteryDetail.Supported)
{
    Console.WriteLine($"  Voltage:          {batteryDetail.Voltage:F3} V");
    Console.WriteLine($"  Amperage:         {batteryDetail.Amperage} mA");
    Console.WriteLine($"  Temperature:      {batteryDetail.Temperature:F1}°C");
    Console.WriteLine($"  Cycle Count:      {batteryDetail.CycleCount}");
    Console.WriteLine($"  Current Capacity: {batteryDetail.CurrentCapacity} mAh");
    Console.WriteLine($"  Max Capacity:     {batteryDetail.MaxCapacity} mAh");
    Console.WriteLine($"  Design Capacity:  {batteryDetail.DesignCapacity} mAh");
    Console.WriteLine($"  Health:           {batteryDetail.Health}%");
    Console.WriteLine($"  AC Watts:         {batteryDetail.AcWatts} W");
    Console.WriteLine($"  Optimized Charge: {batteryDetail.OptimizedChargingEngaged}");
}
else
{
    Console.WriteLine("  Battery detail not supported.");
}
Console.WriteLine();

Console.WriteLine("### 9b. Battery Generic (IOPowerSources + IORegistry 統合) ###");
var bg = PlatformProvider.GetBatteryGeneric();
if (!bg.Supported)
{
    Console.WriteLine("  Battery not supported.");
}
else
{
    // ユーザー表示向けサマリ
    Console.WriteLine($"  [Summary]");
    Console.WriteLine($"  Name:             {bg.Name}");
    Console.WriteLine($"  State:            {bg.PowerSourceState}");
    Console.WriteLine($"  Charging:         {bg.IsCharging} / Charged: {bg.IsCharged}");
    Console.WriteLine($"  Percent:          {bg.BatteryPercent}%");
    Console.WriteLine($"  Capacity:         {bg.CurrentCapacity} / {bg.MaxCapacity} mAh");
    Console.WriteLine($"  Health:           {bg.BatteryHealth ?? "N/A"}");
    if (bg.TimeToEmpty >= 0) Console.WriteLine($"  Time to Empty:    {bg.TimeToEmpty} min");
    if (bg.TimeToFullCharge >= 0) Console.WriteLine($"  Time to Full:     {bg.TimeToFullCharge} min");

    if (bg.DetailSupported)
    {
        // 診断監視向け詳細情報
        Console.WriteLine($"  [Detail]");
        Console.WriteLine($"  Voltage:          {bg.Voltage:F3} V");
        Console.WriteLine($"  Amperage:         {bg.Amperage} mA");
        Console.WriteLine($"  Temperature:      {bg.Temperature:F1}°C");
        Console.WriteLine($"  Cycle Count:      {bg.CycleCount}");
        Console.WriteLine($"  Design Capacity:  {bg.DesignCapacity} mAh");
        Console.WriteLine($"  Health:           {bg.Health}%");
        Console.WriteLine($"  AC Watts:         {bg.AcWatts} W");
        Console.WriteLine($"  Charging Current: {bg.ChargingCurrent} mA");
        Console.WriteLine($"  Charging Voltage: {bg.ChargingVoltage} mV");
        Console.WriteLine($"  Optimized Charge: {bg.OptimizedChargingEngaged}");
    }
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 10. Apple Silicon Energy / Power (IOReport)
// 累積エネルギー (J) と瞬間消費電力 (W) を表示する。
// 初回スナップショットを保存し、1秒後に差分 / 経過時間で瞬間電力を算出する。
// ---------------------------------------------------------------------------
Console.WriteLine("### 10. Apple Silicon Energy / Power (IOReport) ###");
var asPower = PlatformProvider.GetAppleSiliconEnergyCounter();
if (asPower.Supported)
{
    // 初回読み取り (ベースライン)
    asPower.Update();
    var prevCpu = asPower.Cpu;
    var prevGpu = asPower.Gpu;
    var prevAne = asPower.Ane;
    var prevRam = asPower.Ram;
    var prevPci = asPower.Pci;
    var prevTime = DateTime.UtcNow;

    Thread.Sleep(1000);

    // 2 回目読み取り
    asPower.Update();
    var elapsed = (DateTime.UtcNow - prevTime).TotalSeconds;

    Console.WriteLine($"  [Cumulative Energy]");
    Console.WriteLine($"  CPU Energy: {asPower.Cpu:F6} J");
    Console.WriteLine($"  GPU Energy: {asPower.Gpu:F6} J");
    Console.WriteLine($"  ANE Energy: {asPower.Ane:F6} J");
    Console.WriteLine($"  RAM Energy: {asPower.Ram:F6} J");
    Console.WriteLine($"  PCI Energy: {asPower.Pci:F6} J");
    Console.WriteLine($"  Total:      {asPower.Total:F6} J");
    Console.WriteLine();
    Console.WriteLine($"  [Instantaneous Power (over {elapsed:F2}s)]");
    Console.WriteLine($"  CPU Power:  {(asPower.Cpu - prevCpu) / elapsed:F2} W");
    Console.WriteLine($"  GPU Power:  {(asPower.Gpu - prevGpu) / elapsed:F2} W");
    Console.WriteLine($"  ANE Power:  {(asPower.Ane - prevAne) / elapsed:F2} W");
    Console.WriteLine($"  RAM Power:  {(asPower.Ram - prevRam) / elapsed:F2} W");
    Console.WriteLine($"  PCI Power:  {(asPower.Pci - prevPci) / elapsed:F2} W");
    var totalPower = (asPower.Total - (prevCpu + prevGpu + prevAne + prevRam + prevPci)) / elapsed;
    Console.WriteLine($"  Total:      {totalPower:F2} W");
}
else
{
    Console.WriteLine("  Apple Silicon not detected.");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 11. Sensors (HardwareMonitor)
// HardwareMonitor.Create() で SMC 接続を確立し、センサーを一括管理する。
// Update() を呼ぶたびに温度・電圧・電力・ファンをまとめて更新する。
// ---------------------------------------------------------------------------
Console.WriteLine("### 11. Sensors (HardwareMonitor) ###");
using var monitor = PlatformProvider.GetHardwareMonitor();
if (monitor is null)
{
    Console.WriteLine("  HardwareMonitor not available (AppleSMC not found).");
}
else
{
    Console.WriteLine($"  Detected at: {monitor.UpdateAt:HH:mm:ss.fff}");
    Console.WriteLine();

    Console.WriteLine("### 11a. Temperature Sensors ###");
    var tempsLimit = Math.Min(10, monitor.Temperatures.Count);
    for (var i = 0; i < tempsLimit; i++)
    {
        var s = monitor.Temperatures[i];
        Console.WriteLine($"  [{s.Key}] {(string.IsNullOrEmpty(s.Description) ? "(no desc)" : s.Description),-40} {s.Value:F1} °C");
    }
    if (monitor.Temperatures.Count > 10)
    {
        Console.WriteLine($"  ... and {monitor.Temperatures.Count - 10} more sensors.");
    }
    Console.WriteLine();

    Console.WriteLine("### 11b. Power Readings ###");
    var powersLimit = Math.Min(10, monitor.Powers.Count);
    for (var i = 0; i < powersLimit; i++)
    {
        var s = monitor.Powers[i];
        Console.WriteLine($"  [{s.Key}] {(string.IsNullOrEmpty(s.Description) ? "(no desc)" : s.Description),-40} {s.Value:F2} W");
    }
    if (monitor.Powers.Count > 10)
    {
        Console.WriteLine($"  ... and {monitor.Powers.Count - 10} more readings.");
    }
    if (monitor.TotalSystemPower is not null)
    {
        Console.WriteLine($"  Total System Power (PSTR): {monitor.TotalSystemPower:F2} W");
    }
    Console.WriteLine();

    Console.WriteLine("### 11c. Voltage Readings ###");
    var voltagesLimit = Math.Min(10, monitor.Voltages.Count);
    for (var i = 0; i < voltagesLimit; i++)
    {
        var s = monitor.Voltages[i];
        Console.WriteLine($"  [{s.Key}] {(string.IsNullOrEmpty(s.Description) ? "(no desc)" : s.Description),-40} {s.Value:F3} V");
    }
    if (monitor.Voltages.Count > 10)
    {
        Console.WriteLine($"  ... and {monitor.Voltages.Count - 10} more readings.");
    }
    Console.WriteLine();

    Console.WriteLine("### 11d. Current Readings ###");
    if (monitor.Currents.Count == 0)
    {
        Console.WriteLine("  No current sensors detected.");
    }
    var currentsLimit = Math.Min(10, monitor.Currents.Count);
    for (var i = 0; i < currentsLimit; i++)
    {
        var s = monitor.Currents[i];
        Console.WriteLine($"  [{s.Key}] {(string.IsNullOrEmpty(s.Description) ? "(no desc)" : s.Description),-40} {s.Value:F3} A");
    }
    if (monitor.Currents.Count > 10)
    {
        Console.WriteLine($"  ... and {monitor.Currents.Count - 10} more readings.");
    }
    Console.WriteLine();

    Console.WriteLine("### 11e. Fans ###");
    if (monitor.Fans.Count == 0)
    {
        Console.WriteLine("  No fans detected.");
    }
    foreach (var fan in monitor.Fans)
    {
        Console.WriteLine($"  Fan {fan.Index}: {fan.ActualRpm:F0} RPM  (min={fan.MinRpm:F0}, max={fan.MaxRpm:F0}, target={fan.TargetRpm:F0})");
    }
}
Console.WriteLine();

Console.WriteLine("=== Done ===");

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static string FormatBytes(ulong bytes) => bytes switch
{
    >= 1UL << 40 => $"{bytes / (double)(1UL << 40):F2} TiB",
    >= 1UL << 30 => $"{bytes / (double)(1UL << 30):F2} GiB",
    >= 1UL << 20 => $"{bytes / (double)(1UL << 20):F2} MiB",
    >= 1UL << 10 => $"{bytes / (double)(1UL << 10):F2} KiB",
    _ => $"{bytes} B",
};

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..(max - 1)] + "…";

// /dev/disk3s1s1 や /dev/disk7s2 から物理ディスク名 (disk3, disk7) を抽出する。
static string? ExtractPhysicalDiskName(string devicePath)
{
    const string prefix = "/dev/";
    if (!devicePath.StartsWith(prefix, StringComparison.Ordinal))
    {
        return null;
    }

    var name = devicePath[prefix.Length..]; // "disk3s1s1"
    var i = 4; // "disk" の長さ分スキップ
    while (i < name.Length && char.IsDigit(name[i]))
    {
        i++;
    }

    return i > 4 ? name[..i] : null; // "disk3"
}
