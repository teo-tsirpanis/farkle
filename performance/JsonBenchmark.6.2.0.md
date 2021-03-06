``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.203
  [Host]     : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT


```
|       Method |    FileName | SyntaxCheck |         Mean |      Error |     StdDev |       Median | Ratio | RatioSD | Rank |    Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------- |------------ |------------ |-------------:|-----------:|-----------:|-------------:|------:|--------:|-----:|---------:|---------:|--------:|-----------:|
| **FarkleStream** |    **big.json** |       **False** | **2,334.204 μs** | **33.4272 μs** | **29.6323 μs** | **2,344.279 μs** |  **0.48** |    **0.01** |    **2** | **183.5938** |  **89.8438** |       **-** | **1100.42 KB** |
| FarkleString |    big.json |       False | 2,272.392 μs | 26.4939 μs | 24.7824 μs | 2,262.986 μs |  0.47 |    0.01 |    1 | 183.5938 |  89.8438 |       - | 1097.14 KB |
|       Chiron |    big.json |       False | 4,815.562 μs | 43.8623 μs | 36.6270 μs | 4,822.368 μs |  1.00 |    0.00 |    3 | 632.8125 | 312.5000 |       - | 3644.49 KB |
|    FsLexYacc |    big.json |       False | 4,846.580 μs | 83.9229 μs | 74.3955 μs | 4,844.323 μs |  1.01 |    0.01 |    3 | 281.2500 | 187.5000 | 93.7500 | 1788.41 KB |
|              |             |             |              |            |            |              |       |         |      |          |          |         |            |
| **FarkleStream** |    **big.json** |        **True** | **1,542.255 μs** | **30.6839 μs** | **79.2050 μs** | **1,517.212 μs** |  **0.55** |    **0.03** |    **2** | **107.4219** |        **-** |       **-** |  **334.13 KB** |
| FarkleString |    big.json |        True | 1,434.395 μs | 23.1755 μs | 19.3526 μs | 1,435.009 μs |  0.51 |    0.01 |    1 | 107.4219 |        - |       - |  330.85 KB |
|       Chiron |    big.json |        True | 2,815.293 μs | 53.0119 μs | 54.4394 μs | 2,821.418 μs |  1.00 |    0.00 |    3 | 128.9063 |        - |       - |  398.89 KB |
|    FsLexYacc |    big.json |        True | 3,940.600 μs | 65.5597 μs | 61.3246 μs | 3,935.275 μs |  1.40 |    0.04 |    4 | 285.1563 | 179.6875 | 93.7500 | 1160.38 KB |
|              |             |             |              |            |            |              |       |         |      |          |          |         |            |
| **FarkleStream** | **medium.json** |       **False** |   **128.772 μs** |  **2.1724 μs** |  **2.0321 μs** |   **128.694 μs** |  **0.54** |    **0.01** |    **1** |  **22.2168** |        **-** |       **-** |    **68.4 KB** |
| FarkleString | medium.json |       False |   127.640 μs |  2.4618 μs |  6.6975 μs |   125.755 μs |  0.51 |    0.01 |    1 |  21.2402 |        - |       - |   65.12 KB |
|       Chiron | medium.json |       False |   238.392 μs |  1.8691 μs |  1.4593 μs |   237.977 μs |  1.00 |    0.00 |    3 |  66.1621 |        - |       - |  203.19 KB |
|    FsLexYacc | medium.json |       False |   223.302 μs |  1.4260 μs |  1.3339 μs |   222.904 μs |  0.94 |    0.01 |    2 |  59.8145 |  19.7754 |       - |  199.48 KB |
|              |             |             |              |            |            |              |       |         |      |          |          |         |            |
| **FarkleStream** | **medium.json** |        **True** |    **75.433 μs** |  **1.4659 μs** |  **1.6882 μs** |    **75.360 μs** |  **0.49** |    **0.01** |    **2** |   **7.3242** |        **-** |       **-** |   **22.63 KB** |
| FarkleString | medium.json |        True |    74.898 μs |  2.3916 μs |  7.0141 μs |    71.895 μs |  0.50 |    0.05 |    1 |   6.2256 |        - |       - |   19.35 KB |
|       Chiron | medium.json |        True |   152.398 μs |  3.0424 μs |  5.6392 μs |   151.856 μs |  1.00 |    0.00 |    3 |   6.5918 |        - |       - |   20.36 KB |
|    FsLexYacc | medium.json |        True |   182.924 μs |  3.4970 μs |  6.6534 μs |   181.500 μs |  1.20 |    0.06 |    4 |  51.5137 |   0.2441 |       - |  159.45 KB |
|              |             |             |              |            |            |              |       |         |      |          |          |         |            |
| **FarkleStream** |  **small.json** |       **False** |    **13.835 μs** |  **0.2897 μs** |  **0.8218 μs** |    **13.619 μs** |  **0.59** |    **0.05** |    **2** |   **3.7842** |        **-** |       **-** |   **11.63 KB** |
| FarkleString |  small.json |       False |    12.679 μs |  0.2528 μs |  0.5654 μs |    12.647 μs |  0.54 |    0.03 |    1 |   2.7161 |        - |       - |    8.34 KB |
|       Chiron |  small.json |       False |    23.520 μs |  0.4938 μs |  1.4248 μs |    23.431 μs |  1.00 |    0.00 |    3 |   4.8218 |        - |       - |   14.81 KB |
|    FsLexYacc |  small.json |       False |    29.142 μs |  0.6800 μs |  1.9835 μs |    28.534 μs |  1.25 |    0.12 |    4 |  37.3535 |   9.3384 |       - |  115.13 KB |
|              |             |             |              |            |            |              |       |         |      |          |          |         |            |
| **FarkleStream** |  **small.json** |        **True** |     **8.867 μs** |  **0.1740 μs** |  **0.2708 μs** |     **8.806 μs** |  **0.50** |    **0.03** |    **1** |   **2.3041** |        **-** |       **-** |     **7.1 KB** |
| FarkleString |  small.json |        True |     8.684 μs |  0.1719 μs |  0.4706 μs |     8.668 μs |  0.51 |    0.04 |    1 |   1.2360 |        - |       - |    3.82 KB |
|       Chiron |  small.json |        True |    17.260 μs |  0.3441 μs |  0.9126 μs |    16.938 μs |  1.00 |    0.00 |    2 |   1.2207 |        - |       - |    3.86 KB |
|    FsLexYacc |  small.json |        True |    25.164 μs |  0.6277 μs |  1.8310 μs |    24.842 μs |  1.46 |    0.12 |    3 |  36.2244 |        - |       - |  111.35 KB |
