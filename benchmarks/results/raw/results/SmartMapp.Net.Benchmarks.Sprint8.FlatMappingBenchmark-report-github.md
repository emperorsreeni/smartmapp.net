```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.200
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  Job-CHEMSH : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                                | Job        | IterationCount | LaunchCount | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------- |----------- |--------------- |------------ |---------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;ISculptor.Map&lt;Flat, FlatDto&gt; — warm&#39; | Job-CHEMSH | 5              | Default     | 66.68 ns |  5.286 ns | 1.373 ns |  1.00 |    0.03 | 0.0197 |     248 B |        1.00 |
|                                       |            |                |             |          |           |          |       |         |        |           |             |
| &#39;ISculptor.Map&lt;Flat, FlatDto&gt; — warm&#39; | ShortRun   | 3              | 1           | 63.27 ns | 22.736 ns | 1.246 ns |  1.00 |    0.02 | 0.0197 |     248 B |        1.00 |
