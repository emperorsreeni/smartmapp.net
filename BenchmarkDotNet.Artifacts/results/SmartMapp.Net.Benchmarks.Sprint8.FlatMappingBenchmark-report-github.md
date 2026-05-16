```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8457)
Unknown processor
.NET SDK 10.0.204
  [Host]     : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  Job-ZTJULE : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                                | Job        | IterationCount | LaunchCount | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------- |----------- |--------------- |------------ |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;ISculptor.Map&lt;Flat, FlatDto&gt; — warm&#39; | Job-ZTJULE | 5              | Default     | 66.44 ns | 17.78 ns | 4.618 ns |  1.00 |    0.09 | 0.0197 |     248 B |        1.00 |
|                                       |            |                |             |          |          |          |       |         |        |           |             |
| &#39;ISculptor.Map&lt;Flat, FlatDto&gt; — warm&#39; | ShortRun   | 3              | 1           | 86.56 ns | 50.08 ns | 2.745 ns |  1.00 |    0.04 | 0.0197 |     248 B |        1.00 |
