```

BenchmarkDotNet v0.15.8, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), Arm64 RyuJIT armv8.0-a


```
| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| SmcAllKeys      | 106.507 ms | 2.4135 ms | 7.1162 ms |  1.00 |    0.10 |         - |          NA |
| SmcFilteredKeys |   3.503 ms | 0.0684 ms | 0.1064 ms |  0.03 |    0.00 |         - |          NA |
