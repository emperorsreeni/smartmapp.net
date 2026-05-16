```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  Job-VOCQEQ : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                                | Job        | IterationCount | LaunchCount | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------- |----------- |--------------- |------------ |---------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;ISculptor.Map&lt;Flat, FlatDto&gt; — warm&#39; | Job-VOCQEQ | 5              | Default     | 61.14 ns |  12.86 ns | 1.990 ns |  1.00 |    0.04 | 0.0197 |     248 B |        1.00 |
|                                       |            |                |             |          |           |          |       |         |        |           |             |
| &#39;ISculptor.Map&lt;Flat, FlatDto&gt; — warm&#39; | ShortRun   | 3              | 1           | 71.77 ns | 135.88 ns | 7.448 ns |  1.01 |    0.13 | 0.0197 |     248 B |        1.00 |
