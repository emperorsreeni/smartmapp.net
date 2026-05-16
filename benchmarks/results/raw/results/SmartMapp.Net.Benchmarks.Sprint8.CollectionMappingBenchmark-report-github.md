```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8246)
Unknown processor
.NET SDK 10.0.200
  [Host]     : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  Job-CHEMSH : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                      | Job        | IterationCount | LaunchCount | Count | Mean       | Error       | StdDev     | Gen0     | Gen1    | Allocated  |
|---------------------------- |----------- |--------------- |------------ |------ |-----------:|------------:|-----------:|---------:|--------:|-----------:|
| **&#39;MapAll&lt;Item[], ItemDto[]&gt;&#39;** | **Job-CHEMSH** | **5**              | **Default**     | **100**   |   **5.986 μs** |   **0.5738 μs** |  **0.0888 μs** |   **1.5984** |  **0.0305** |   **19.59 KB** |
| &#39;MapAll&lt;Item[], ItemDto[]&gt;&#39; | ShortRun   | 3              | 1           | 100   |   6.070 μs |   1.1489 μs |  0.0630 μs |   1.5945 |  0.0305 |   19.59 KB |
| **&#39;MapAll&lt;Item[], ItemDto[]&gt;&#39;** | **Job-CHEMSH** | **5**              | **Default**     | **1000**  |  **61.270 μs** |   **5.0529 μs** |  **1.3122 μs** |  **15.9302** |  **2.8076** |  **195.37 KB** |
| &#39;MapAll&lt;Item[], ItemDto[]&gt;&#39; | ShortRun   | 3              | 1           | 1000  |  62.754 μs |  69.1063 μs |  3.7880 μs |  15.9302 |  2.8076 |  195.37 KB |
| **&#39;MapAll&lt;Item[], ItemDto[]&gt;&#39;** | **Job-CHEMSH** | **5**              | **Default**     | **10000** | **738.068 μs** |  **45.0511 μs** | **11.6996 μs** | **159.1797** | **68.3594** | **1953.18 KB** |
| &#39;MapAll&lt;Item[], ItemDto[]&gt;&#39; | ShortRun   | 3              | 1           | 10000 | 706.340 μs | 327.1362 μs | 17.9314 μs | 159.1797 | 68.8477 | 1953.18 KB |
