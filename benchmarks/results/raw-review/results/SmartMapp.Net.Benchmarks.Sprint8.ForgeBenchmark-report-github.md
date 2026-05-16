```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.200
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  Job-HHWHUB : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

IterationCount=3  

```
| Method                                                | Job        | LaunchCount | WarmupCount | Mean      | Error     | StdDev    | Gen0     | Gen1    | Allocated  |
|------------------------------------------------------ |----------- |------------ |------------ |----------:|----------:|----------:|---------:|--------:|-----------:|
| &#39;Forge() — 1 blueprint (4 pairs via BundleBlueprint)&#39; | Job-HHWHUB | Default     | 2           |  4.815 ms |  1.128 ms | 0.0618 ms |  15.6250 |  7.8125 |  248.15 KB |
| &#39;Forge() — 100 inline Bind&lt;,&gt;() pairs&#39;                | Job-HHWHUB | Default     | 2           | 34.940 ms | 78.507 ms | 4.3032 ms | 166.6667 | 83.3333 | 2079.17 KB |
| &#39;Forge() — 1 blueprint (4 pairs via BundleBlueprint)&#39; | ShortRun   | 1           | 3           |  4.702 ms | 13.049 ms | 0.7153 ms |  15.6250 |  7.8125 |  248.23 KB |
| &#39;Forge() — 100 inline Bind&lt;,&gt;() pairs&#39;                | ShortRun   | 1           | 3           | 31.742 ms | 14.243 ms | 0.7807 ms | 166.6667 | 83.3333 |  2079.3 KB |
