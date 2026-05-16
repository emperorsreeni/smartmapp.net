```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.200
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  Job-CHEMSH : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                                                  | Job        | IterationCount | LaunchCount | Mean     | Error    | StdDev   | Gen0   | Allocated |
|-------------------------------------------------------- |----------- |--------------- |------------ |---------:|---------:|---------:|-------:|----------:|
| &#39;ISculptor.Map&lt;NestedOrder, FlatDto&gt; (3-level flatten)&#39; | Job-CHEMSH | 5              | Default     | 66.45 ns | 2.607 ns | 0.404 ns | 0.0178 |     224 B |
| &#39;ISculptor.Map&lt;NestedOrder, FlatDto&gt; (3-level flatten)&#39; | ShortRun   | 3              | 1           | 68.43 ns | 1.739 ns | 0.095 ns | 0.0178 |     224 B |
