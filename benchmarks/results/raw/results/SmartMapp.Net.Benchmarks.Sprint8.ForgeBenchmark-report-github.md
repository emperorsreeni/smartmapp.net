```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.200
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  Job-AMTLFA : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

IterationCount=3  

```
| Method                                                | Job        | LaunchCount | WarmupCount | Mean     | Error    | StdDev    | Gen0    | Gen1   | Allocated |
|------------------------------------------------------ |----------- |------------ |------------ |---------:|---------:|----------:|--------:|-------:|----------:|
| &#39;Forge() — 1 blueprint (4 pairs via BundleBlueprint)&#39; | Job-AMTLFA | Default     | 2           | 6.213 ms | 3.717 ms | 0.2038 ms | 15.6250 | 7.8125 | 248.15 KB |
| &#39;Forge() — 100 inline Bind&lt;,&gt;() pairs&#39;                | Job-AMTLFA | Default     | 2           |       NA |       NA |        NA |      NA |     NA |        NA |
| &#39;Forge() — 1 blueprint (4 pairs via BundleBlueprint)&#39; | ShortRun   | 1           | 3           | 6.048 ms | 1.621 ms | 0.0888 ms | 15.6250 | 7.8125 | 248.62 KB |
| &#39;Forge() — 100 inline Bind&lt;,&gt;() pairs&#39;                | ShortRun   | 1           | 3           |       NA |       NA |        NA |      NA |     NA |        NA |

Benchmarks with issues:
  ForgeBenchmark.'Forge() — 100 inline Bind<,>() pairs': Job-AMTLFA(IterationCount=3, WarmupCount=2)
  ForgeBenchmark.'Forge() — 100 inline Bind<,>() pairs': ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3)
