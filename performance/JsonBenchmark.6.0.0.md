``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.103
  [Host]     : .NET Core 5.0.3 (CoreCLR 5.0.321.7212, CoreFX 5.0.321.7212), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 5.0.3 (CoreCLR 5.0.321.7212, CoreFX 5.0.321.7212), X64 RyuJIT


```
|       Method |    FileName | SyntaxCheck |         Mean |      Error |     StdDev | Ratio | RatioSD | Rank |    Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------- |------------ |------------ |-------------:|-----------:|-----------:|------:|--------:|-----:|---------:|---------:|--------:|-----------:|
| **FarkleStream** |    **big.json** |       **False** | **2,463.226 μs** | **11.5860 μs** | **10.8375 μs** |  **0.51** |    **0.01** |    **2** | **187.5000** |  **93.7500** |       **-** | **1100.45 KB** |
| FarkleString |    big.json |       False | 2,403.991 μs | 20.6867 μs | 19.3504 μs |  0.50 |    0.01 |    1 | 183.5938 |  89.8438 |       - | 1097.16 KB |
|       Chiron |    big.json |       False | 4,831.729 μs | 51.7450 μs | 48.4023 μs |  1.00 |    0.00 |    4 | 632.8125 | 312.5000 |  7.8125 | 3644.49 KB |
|    FsLexYacc |    big.json |       False | 4,253.249 μs | 48.6455 μs | 40.6212 μs |  0.88 |    0.01 |    3 | 281.2500 | 187.5000 | 93.7500 | 1788.43 KB |
|              |             |             |              |            |            |       |         |      |          |          |         |            |
| **FarkleStream** |    **big.json** |        **True** | **1,494.527 μs** | **20.8877 μs** | **20.5145 μs** |  **0.54** |    **0.01** |    **2** | **107.4219** |        **-** |       **-** |  **334.16 KB** |
| FarkleString |    big.json |        True | 1,402.947 μs | 10.5754 μs |  9.8923 μs |  0.51 |    0.00 |    1 | 107.4219 |        - |       - |  330.88 KB |
|       Chiron |    big.json |        True | 2,774.482 μs | 19.2371 μs | 16.0639 μs |  1.00 |    0.00 |    3 | 128.9063 |        - |       - |  398.89 KB |
|    FsLexYacc |    big.json |        True | 3,455.474 μs | 16.7540 μs | 14.8520 μs |  1.25 |    0.01 |    4 | 285.1563 | 179.6875 | 93.7500 | 1160.41 KB |
|              |             |             |              |            |            |       |         |      |          |          |         |            |
| **FarkleStream** | **medium.json** |       **False** |   **129.233 μs** |  **1.3110 μs** |  **1.1622 μs** |  **0.55** |    **0.01** |    **2** |  **22.2168** |        **-** |       **-** |   **68.42 KB** |
| FarkleString | medium.json |       False |   124.557 μs |  0.8707 μs |  0.7719 μs |  0.53 |    0.01 |    1 |  21.2402 |        - |       - |   65.14 KB |
|       Chiron | medium.json |       False |   235.384 μs |  2.6215 μs |  2.1890 μs |  1.00 |    0.00 |    4 |  66.1621 |        - |       - |  203.19 KB |
|    FsLexYacc | medium.json |       False |   227.832 μs |  1.5848 μs |  1.4049 μs |  0.97 |    0.01 |    3 |  59.8145 |  19.7754 |       - |   199.5 KB |
|              |             |             |              |            |            |       |         |      |          |          |         |            |
| **FarkleStream** | **medium.json** |        **True** |    **78.133 μs** |  **0.9106 μs** |  **0.7604 μs** |  **0.53** |    **0.01** |    **2** |   **7.3242** |        **-** |       **-** |   **22.66 KB** |
| FarkleString | medium.json |        True |    75.795 μs |  0.7730 μs |  0.7231 μs |  0.52 |    0.01 |    1 |   6.2256 |        - |       - |   19.38 KB |
|       Chiron | medium.json |        True |   146.527 μs |  1.7791 μs |  1.5771 μs |  1.00 |    0.00 |    3 |   6.5918 |        - |       - |   20.36 KB |
|    FsLexYacc | medium.json |        True |   183.847 μs |  1.2226 μs |  1.0838 μs |  1.25 |    0.02 |    4 |  51.7578 |  12.9395 |       - |  159.47 KB |
|              |             |             |              |            |            |       |         |      |          |          |         |            |
| **FarkleStream** |  **small.json** |       **False** |    **13.583 μs** |  **0.1860 μs** |  **0.1553 μs** |  **0.62** |    **0.01** |    **2** |   **3.7994** |        **-** |       **-** |   **11.65 KB** |
| FarkleString |  small.json |       False |    12.719 μs |  0.2394 μs |  0.2122 μs |  0.58 |    0.01 |    1 |   2.7313 |        - |       - |    8.37 KB |
|       Chiron |  small.json |       False |    21.995 μs |  0.1769 μs |  0.1568 μs |  1.00 |    0.00 |    3 |   4.8218 |        - |       - |   14.81 KB |
|    FsLexYacc |  small.json |       False |    26.484 μs |  0.3436 μs |  0.3214 μs |  1.20 |    0.02 |    4 |  37.3535 |   9.3384 |       - |  115.16 KB |
|              |             |             |              |            |            |       |         |      |          |          |         |            |
| **FarkleStream** |  **small.json** |        **True** |     **9.293 μs** |  **0.0748 μs** |  **0.0699 μs** |  **0.58** |    **0.01** |    **2** |   **2.3193** |        **-** |       **-** |    **7.13 KB** |
| FarkleString |  small.json |        True |     8.473 μs |  0.1352 μs |  0.1199 μs |  0.53 |    0.01 |    1 |   1.2512 |        - |       - |    3.84 KB |
|       Chiron |  small.json |        True |    15.925 μs |  0.1662 μs |  0.1473 μs |  1.00 |    0.00 |    3 |   1.2512 |        - |       - |    3.86 KB |
|    FsLexYacc |  small.json |        True |    23.691 μs |  0.1649 μs |  0.1287 μs |  1.49 |    0.02 |    4 |  36.1328 |        - |       - |  111.38 KB |