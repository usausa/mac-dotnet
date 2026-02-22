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
        Console.WriteLine($"  [{level.Index}] {level.Name}: PhysicalCpu={level.PhysicalCpu}, LogicalCpu={level.LogicalCpu}, L2={FormatBytes((ulong)level.L2CacheSize)}");
    }
    Console.WriteLine();
}

// ---------------------------------------------------------------------------
// 3. CPU Usage  (constructor takes first snapshot; Update() after interval computes delta)
// ---------------------------------------------------------------------------
Console.WriteLine("### 3. CPU Usage ###");
Console.WriteLine("  Measuring (500ms)...");
var cpu = PlatformProvider.GetCpuUsageStat();
Thread.Sleep(500);
cpu.Update();
Console.WriteLine($"  User:             {cpu.UserLoad:P2}");
Console.WriteLine($"  System:           {cpu.SystemLoad:P2}");
Console.WriteLine($"  Idle:             {cpu.IdleLoad:P2}");
Console.WriteLine($"  Total:            {cpu.TotalLoad:P2}");
if (cpu.ECoreUsage is not null)
{
    Console.WriteLine($"  E-Core Average:   {cpu.ECoreUsage:P2}");
}
if (cpu.PCoreUsage is not null)
{
    Console.WriteLine($"  P-Core Average:   {cpu.PCoreUsage:P2}");
}
Console.WriteLine($"  Per-Core ({cpu.UsagePerCore.Length} cores):");
for (var i = 0; i < Math.Min(cpu.UsagePerCore.Length, 12); i++)
{
    Console.WriteLine($"    Core {i,2}: {cpu.UsagePerCore[i]:P2}");
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
// 6. Network Interfaces (デフォルト: macOS System Settings のネットワーク設定と同じサービスのみ)
// 全インターフェースを取得したい場合は GetNetworkInterfaces(includeAll: true)
// ---------------------------------------------------------------------------
Console.WriteLine("### 6. Network Interfaces ###");
var interfaces = PlatformProvider.GetNetworkInterfaces();
foreach (var iface in interfaces)
{
    Console.WriteLine($"  [{iface.Name}] {iface.DisplayName} - {iface.State}");
    if (!string.IsNullOrEmpty(iface.MacAddress))
    {
        Console.WriteLine($"    MAC:    {iface.MacAddress}");
    }
    foreach (var addr in iface.IPv4Addresses)
    {
        Console.WriteLine($"    IPv4:   {addr.Address}/{addr.PrefixLength}");
    }
    foreach (var addr in iface.IPv6Addresses)
    {
        Console.WriteLine($"    IPv6:   {addr.Address}/{addr.PrefixLength}");
    }
    Console.WriteLine($"    RX:     {FormatBytes((ulong)iface.RxBytes)} ({iface.RxPackets} pkts, {iface.RxErrors} err)");
    Console.WriteLine($"    TX:     {FormatBytes((ulong)iface.TxBytes)} ({iface.TxPackets} pkts, {iface.TxErrors} err)");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 7. Processes (Top 10 by memory)
// ---------------------------------------------------------------------------
Console.WriteLine("### 7. Processes (Top 10 by RSS) ###");
var processes = PlatformProvider.GetProcesses();
var processArray = new MacDotNet.SystemInfo.ProcessEntry[processes.Count];
for (var i = 0; i < processes.Count; i++) processArray[i] = processes[i];
Array.Sort(processArray, static (a, b) => b.ResidentSize.CompareTo(a.ResidentSize));
var top10Count = Math.Min(10, processArray.Length);
Console.WriteLine($"  {"PID",6} {"Name",-30} {"RSS",10} {"VSZ",12} {"Threads",8}");
for (var i = 0; i < top10Count; i++)
{
    var p = processArray[i];
    Console.WriteLine($"  {p.Pid,6} {Truncate(p.Name, 30),-30} {FormatBytes((ulong)p.ResidentSize),10} {FormatBytes((ulong)p.VirtualSize),12} {p.ThreadCount,8}");
}
Console.WriteLine($"  Total processes: {processes.Count}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 8. GPU
// ---------------------------------------------------------------------------
Console.WriteLine("### 8. GPU ###");
var gpus = PlatformProvider.GetGpus();
if (gpus.Count == 0)
{
    Console.WriteLine("  No GPU found.");
}
foreach (var gpu in gpus)
{
    Console.WriteLine($"  {gpu.Model ?? "Unknown GPU"}");
    Console.WriteLine($"    Cores:       {gpu.CoreCount}");
    if (gpu.Performance is not null)
    {
        var perf = gpu.Performance;
        Console.WriteLine($"    Utilization: {perf.DeviceUtilization}%");
        if (perf.Temperature > 0)
        {
            Console.WriteLine($"    Temperature: {perf.Temperature}°C");
        }
    }
    if (gpu.Temperature is not null)
    {
        Console.WriteLine($"    Temperature: {gpu.Temperature}°C");
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
// 10. Apple Silicon Power  (constructor takes first snapshot; Update() after interval computes delta)
// ---------------------------------------------------------------------------
Console.WriteLine("### 10. Apple Silicon Power ###");
var asPower = PlatformProvider.GetAppleSiliconPower();
if (asPower.Supported)
{
    Thread.Sleep(500);
    asPower.Update();
    Console.WriteLine($"  CPU Power:  {asPower.CpuPower:F2} W");
    Console.WriteLine($"  GPU Power:  {asPower.GpuPower:F2} W");
    Console.WriteLine($"  ANE Power:  {asPower.AnePower:F2} W");
    Console.WriteLine($"  RAM Power:  {asPower.RamPower:F2} W");
    Console.WriteLine($"  Total:      {asPower.TotalPower:F2} W");
}
else
{
    Console.WriteLine("  Apple Silicon not detected.");
}
Console.WriteLine();

// ---------------------------------------------------------------------------
// 11. Sensors (SMC)
// ---------------------------------------------------------------------------
Console.WriteLine("### 11. Temperature Sensors ###");
var temps = PlatformProvider.GetTemperatureSensors();
var tempsLimit = Math.Min(10, temps.Count);
for (var i = 0; i < tempsLimit; i++)
{
    var s = temps[i];
    Console.WriteLine($"  [{s.Key}] {(string.IsNullOrEmpty(s.Description) ? "(no desc)" : s.Description),-40} {s.Value:F1}");
}
if (temps.Count > 10)
{
    Console.WriteLine($"  ... and {temps.Count - 10} more sensors.");
}
Console.WriteLine();

Console.WriteLine("### 11a. Power Readings ###");
var powers = PlatformProvider.GetPowerReadings();
var powersLimit = Math.Min(10, powers.Count);
for (var i = 0; i < powersLimit; i++)
{
    var s = powers[i];
    Console.WriteLine($"  [{s.Key}] {(string.IsNullOrEmpty(s.Description) ? "(no desc)" : s.Description),-40} {s.Value:F2}");
}
if (powers.Count > 10)
{
    Console.WriteLine($"  ... and {powers.Count - 10} more readings.");
}
Console.WriteLine();

Console.WriteLine("### 11b. Voltage Readings ###");
var voltages = PlatformProvider.GetVoltageReadings();
var voltagesLimit = Math.Min(10, voltages.Count);
for (var i = 0; i < voltagesLimit; i++)
{
    var s = voltages[i];
    Console.WriteLine($"  [{s.Key}] {(string.IsNullOrEmpty(s.Description) ? "(no desc)" : s.Description),-40} {s.Value:F3}");
}
if (voltages.Count > 10)
{
    Console.WriteLine($"  ... and {voltages.Count - 10} more readings.");
}
Console.WriteLine();

Console.WriteLine("### 11c. Fans ###");
var fans = PlatformProvider.GetFans();
if (fans.Count == 0)
{
    Console.WriteLine("  No fans detected.");
}
foreach (var fan in fans)
{
    Console.WriteLine($"  Fan {fan.Index}: {fan.ActualRpm:F0} RPM (min={fan.MinRpm:F0}, max={fan.MaxRpm:F0}, target={fan.TargetRpm:F0})");
}

var totalPower = PlatformProvider.GetTotalSystemPower();
if (totalPower is not null)
{
    Console.WriteLine($"  Total System Power (PSTR): {totalPower:F2} W");
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
