```

BenchmarkDotNet v0.15.8, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a


```
| Method              | Mean                | Error             | StdDev            | Allocated |
|-------------------- |--------------------:|------------------:|------------------:|----------:|
| CpuStat             |       4,529.9787 ns |       144.3990 ns |       425.7637 ns |         - |
| MemoryStat          |       1,025.3240 ns |         3.9527 ns |         3.5039 ns |         - |
| SwapUsage           |         308.0984 ns |         1.0758 ns |         1.0063 ns |         - |
| LoadAverage         |         313.3441 ns |         1.0267 ns |         0.9604 ns |         - |
| Uptime              |         333.6935 ns |         1.2445 ns |         1.1032 ns |         - |
| FileHandleStat      |         757.0994 ns |         3.5920 ns |         3.3599 ns |         - |
| DiskStat            |     325,381.6057 ns |     2,193.5247 ns |     1,831.6925 ns |         - |
| FileSystemStat      |       8,520.9071 ns |        31.4680 ns |        27.8956 ns |      80 B |
| NetworkStat         |      87,326.4051 ns |       239.0118 ns |       211.8778 ns |         - |
| ProcessSummary      |     567,186.9618 ns |       886.9692 ns |       740.6594 ns |         - |
| CpuFrequency        |   1,031,353.6137 ns |    13,179.6391 ns |    12,328.2426 ns |         - |
| GpuDevices          |      21,951.8851 ns |       140.4604 ns |       131.3868 ns |         - |
| PowerStat           |   1,600,529.6794 ns |    10,063.5653 ns |     8,921.0886 ns |         - |
| PowerManagementStat |     187,288.8962 ns |     2,695.3101 ns |     2,521.1947 ns |         - |
| BatteryDevice       |           0.3722 ns |         0.0030 ns |         0.0026 ns |         - |
| MainsDevice         |     162,775.0244 ns |       993.8029 ns |       829.8704 ns |         - |
| SmcMonitor          | 107,274,326.1700 ns | 2,360,771.5598 ns | 6,960,788.7138 ns |         - |
| All                 | 105,297,424.6700 ns | 2,325,851.1454 ns | 6,857,825.0767 ns |      80 B |
