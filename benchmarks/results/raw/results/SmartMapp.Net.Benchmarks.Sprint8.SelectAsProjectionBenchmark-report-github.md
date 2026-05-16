```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.200
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  Job-CHEMSH : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                                                | Job        | IterationCount | LaunchCount | Mean            | Error          | StdDev        | Ratio     | RatioSD  | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------ |----------- |--------------- |------------ |----------------:|---------------:|--------------:|----------:|---------:|-------:|----------:|------------:|
| &#39;GetProjection&lt;NestedOrder, FlatDto&gt; — cached&#39;        | Job-CHEMSH | 5              | Default     |        16.08 ns |       8.988 ns |      2.334 ns |      1.02 |     0.19 |      - |         - |          NA |
| &#39;SelectAs&lt;FlatDto&gt;().ToList() — 100 rows (in-memory)&#39; | Job-CHEMSH | 5              | Default     |   907,057.91 ns |  82,390.195 ns | 12,749.974 ns | 57,352.71 | 7,648.27 | 1.9531 |   26952 B |          NA |
|                                                       |            |                |             |                 |                |               |           |          |        |           |             |
| &#39;GetProjection&lt;NestedOrder, FlatDto&gt; — cached&#39;        | ShortRun   | 3              | 1           |        13.56 ns |       3.279 ns |      0.180 ns |      1.00 |     0.02 |      - |         - |          NA |
| &#39;SelectAs&lt;FlatDto&gt;().ToList() — 100 rows (in-memory)&#39; | ShortRun   | 3              | 1           | 1,017,041.24 ns | 407,245.476 ns | 22,322.499 ns | 74,986.94 | 1,667.68 | 1.9531 |   26936 B |          NA |
