```

BenchmarkDotNet v0.15.8, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a


```
| Method              | Mean                | Error             | StdDev            | Allocated |
|-------------------- |--------------------:|------------------:|------------------:|----------:|
| CpuStat             |       4,485.9068 ns |       149.5395 ns |       440.9207 ns |         - |
| MemoryStat          |       1,025.2814 ns |         3.1860 ns |         2.9802 ns |         - |
| SwapUsage           |         310.9781 ns |         1.4270 ns |         1.3348 ns |         - |
| LoadAverage         |         313.6056 ns |         1.0998 ns |         1.0288 ns |         - |
| Uptime              |         335.2212 ns |         1.0816 ns |         0.9588 ns |         - |
| FileHandleStat      |         764.6539 ns |         2.7220 ns |         2.5462 ns |         - |
| DiskStat            |     328,993.6872 ns |     3,802.4604 ns |     3,370.7821 ns |         - |
| FileSystemStat      |       8,320.8839 ns |        24.1595 ns |        22.5988 ns |         - |
| NetworkStat         |      87,359.4245 ns |       292.9842 ns |       274.0576 ns |         - |
| ProcessSummary      |     570,347.8137 ns |     1,073.4210 ns |     1,004.0787 ns |         - |
| CpuFrequency        |   1,031,281.9435 ns |    13,375.3682 ns |    12,511.3277 ns |         - |
| GpuDevices          |      21,886.7503 ns |       143.8038 ns |       134.5142 ns |         - |
| PowerStat           |   1,603,966.5368 ns |     8,962.1198 ns |     8,383.1724 ns |         - |
| PowerManagementStat |     186,506.1544 ns |     2,678.7219 ns |     2,505.6782 ns |         - |
| BatteryDevice       |           0.3714 ns |         0.0012 ns |         0.0011 ns |         - |
| MainsDevice         |     163,270.0460 ns |       692.0893 ns |       647.3808 ns |         - |
| SmcMonitor          | 107,349,426.0099 ns | 2,143,445.8168 ns | 5,758,228.4183 ns |         - |
| All                 | 105,287,017.1600 ns | 2,160,359.8940 ns | 6,369,870.3526 ns |         - |
