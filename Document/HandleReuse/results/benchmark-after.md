```

BenchmarkDotNet v0.15.8, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a


```
| Method              | Mean                | Error             | StdDev            | Allocated |
|-------------------- |--------------------:|------------------:|------------------:|----------:|
| CpuStat             |       4,606.2153 ns |       139.9621 ns |       412.6813 ns |         - |
| MemoryStat          |       1,020.9814 ns |         2.2697 ns |         2.1231 ns |         - |
| SwapUsage           |         308.2492 ns |         1.1842 ns |         1.1077 ns |         - |
| LoadAverage         |         312.9019 ns |         1.4250 ns |         1.3330 ns |         - |
| Uptime              |         333.8387 ns |         0.9372 ns |         0.8766 ns |         - |
| FileHandleStat      |         757.1785 ns |         5.8447 ns |         5.1812 ns |         - |
| DiskStat            |     325,626.2165 ns |     2,703.0872 ns |     2,396.2164 ns |         - |
| FileSystemStat      |       8,486.6848 ns |        21.7474 ns |        19.2785 ns |      80 B |
| NetworkStat         |   4,318,054.7840 ns |    68,748.6995 ns |    60,943.9314 ns |    1168 B |
| ProcessSummary      |     570,562.3585 ns |       957.2102 ns |       799.3139 ns |         - |
| CpuFrequency        |   1,027,610.6996 ns |    12,982.4050 ns |    12,143.7497 ns |         - |
| GpuDevices          |      21,909.3157 ns |        76.8890 ns |        68.1601 ns |         - |
| PowerStat           |   1,604,182.0040 ns |    10,582.4856 ns |     9,898.8636 ns |         - |
| PowerManagementStat |     184,788.7175 ns |     1,201.4059 ns |       937.9787 ns |         - |
| BatteryDevice       |           0.3886 ns |         0.0095 ns |         0.0089 ns |         - |
| MainsDevice         |     163,075.1561 ns |       989.8562 ns |       877.4817 ns |         - |
| SmcMonitor          | 107,219,095.1820 ns | 2,406,394.7657 ns | 7,095,309.7756 ns |         - |
| All                 | 122,988,217.2540 ns | 2,626,842.2190 ns | 7,745,304.1125 ns |    1248 B |
