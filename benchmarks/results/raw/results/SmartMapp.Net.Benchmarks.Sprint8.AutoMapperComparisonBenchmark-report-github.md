```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.200
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  Job-CHEMSH : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                          | Job        | IterationCount | LaunchCount | Mean     | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |----------- |--------------- |------------ |---------:|----------:|---------:|------:|-------:|----------:|------------:|
| &#39;SmartMapp.Net — ISculptor.Map&#39; | Job-CHEMSH | 5              | Default     | 64.20 ns |  1.870 ns | 0.486 ns |  1.00 | 0.0197 |     248 B |        1.00 |
| &#39;AutoMapper — IMapper.Map&lt;,&gt;&#39;   | Job-CHEMSH | 5              | Default     | 74.20 ns |  1.363 ns | 0.354 ns |  1.16 | 0.0083 |     104 B |        0.42 |
|                                 |            |                |             |          |           |          |       |        |           |             |
| &#39;SmartMapp.Net — ISculptor.Map&#39; | ShortRun   | 3              | 1           | 65.44 ns | 11.037 ns | 0.605 ns |  1.00 | 0.0197 |     248 B |        1.00 |
| &#39;AutoMapper — IMapper.Map&lt;,&gt;&#39;   | ShortRun   | 3              | 1           | 74.67 ns |  3.556 ns | 0.195 ns |  1.14 | 0.0083 |     104 B |        0.42 |
