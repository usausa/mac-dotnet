```

BenchmarkDotNet v0.15.8, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a


```
| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| SmcAllKeys      | 106.961 ms | 2.2243 ms | 6.5585 ms |  1.00 |    0.09 |         - |          NA |
| SmcFilteredKeys |   3.466 ms | 0.0682 ms | 0.1580 ms |  0.03 |    0.00 |         - |          NA |
