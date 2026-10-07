```

BenchmarkDotNet v0.15.8, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a


```
| Method              | Mean                | Error             | StdDev            | Median              | Allocated |
|-------------------- |--------------------:|------------------:|------------------:|--------------------:|----------:|
| CpuStat             |       5,015.1853 ns |       134.0525 ns |       395.2569 ns |       4,867.7142 ns |         - |
| MemoryStat          |       1,975.3923 ns |         4.8696 ns |         4.5550 ns |       1,974.7038 ns |         - |
| SwapUsage           |         617.8647 ns |         3.7124 ns |         3.4726 ns |         619.0155 ns |         - |
| LoadAverage         |         312.6934 ns |         1.2368 ns |         1.0964 ns |         312.5529 ns |         - |
| Uptime              |         674.4284 ns |         2.3387 ns |         2.1876 ns |         673.3488 ns |         - |
| FileHandleStat      |       1,654.5573 ns |         8.7330 ns |         8.1688 ns |       1,656.1920 ns |         - |
| DiskStat            |     333,491.3186 ns |     3,825.8290 ns |     3,391.4977 ns |     332,476.3186 ns |         - |
| FileSystemStat      |       8,508.0319 ns |        30.9805 ns |        27.4634 ns |       8,513.7364 ns |      80 B |
| NetworkStat         |   4,269,726.6302 ns |    41,727.2369 ns |    39,031.6833 ns |   4,255,709.9688 ns |    1168 B |
| ProcessSummary      |     568,219.2234 ns |     1,030.9154 ns |       913.8796 ns |     568,116.4346 ns |         - |
| CpuFrequency        |  37,314,996.5659 ns |   155,123.0964 ns |   129,534.8111 ns |  37,299,502.9286 ns |     400 B |
| GpuDevices          |      29,096.2285 ns |       180.3011 ns |       150.5596 ns |      29,037.0916 ns |         - |
| PowerStat           |  39,694,511.9011 ns |   194,511.1840 ns |   172,429.0982 ns |  39,684,413.4615 ns |   22304 B |
| PowerManagementStat |     190,822.9966 ns |     1,735.3021 ns |     1,538.3001 ns |     190,596.4355 ns |         - |
| BatteryDevice       |           0.6806 ns |         0.0013 ns |         0.0012 ns |           0.6807 ns |         - |
| MainsDevice         |     163,108.2906 ns |       956.2956 ns |       894.5195 ns |     163,129.1809 ns |      40 B |
| SmcMonitor          | 108,651,617.4392 ns | 2,166,376.1457 ns | 6,285,050.4720 ns | 108,206,016.6000 ns |         - |
| All                 | 170,193,211.8611 ns | 2,470,189.9829 ns | 1,928,561.8569 ns | 169,597,791.6667 ns |   23992 B |
