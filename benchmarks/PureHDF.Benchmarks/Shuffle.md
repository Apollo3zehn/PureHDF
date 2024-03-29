// * Summary *

BenchmarkDotNet=v0.13.3, OS=ubuntu 22.10
11th Gen Intel Core i7-1165G7 2.80GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
2

|  Method |        N |             Mean |          Error |         StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|-------- |--------- |-----------------:|---------------:|---------------:|------:|--------:|-------:|----------:|------------:|
| Generic |        1 |         10.17 ns |       0.140 ns |       0.131 ns |  1.00 |    0.00 |      - |         - |          NA |
|    SSE2 |        1 |         13.87 ns |       0.101 ns |       0.084 ns |  1.36 |    0.02 |      - |         - |          NA |
|    AVX2 |        1 |         12.51 ns |       0.230 ns |       0.192 ns |  1.23 |    0.02 |      - |         - |          NA |
|         |          |                  |                |                |       |         |        |           |             |
| Generic |      100 |        478.56 ns |       4.564 ns |       4.046 ns |  1.00 |    0.00 |      - |         - |          NA |
|    SSE2 |      100 |        152.54 ns |       1.878 ns |       1.665 ns |  0.32 |    0.00 | 0.0484 |     304 B |          NA |
|    AVX2 |      100 |        127.88 ns |       0.638 ns |       0.498 ns |  0.27 |    0.00 | 0.0892 |     560 B |          NA |
|         |          |                  |                |                |       |         |        |           |             |
| Generic |    10000 |     46,377.88 ns |     703.841 ns |     658.374 ns |  1.00 |    0.00 |      - |         - |          NA |
|    SSE2 |    10000 |     10,881.03 ns |      34.741 ns |      27.123 ns |  0.24 |    0.00 | 0.0458 |     304 B |          NA |
|    AVX2 |    10000 |      7,525.08 ns |     106.731 ns |     109.605 ns |  0.16 |    0.00 | 0.0763 |     560 B |          NA |
|         |          |                  |                |                |       |         |        |           |             |
| Generic |  1000000 |  4,610,980.58 ns |  42,725.305 ns |  35,677.565 ns |  1.00 |    0.00 |      - |       7 B |        1.00 |
|    SSE2 |  1000000 |  1,199,135.12 ns |  23,912.982 ns |  31,923.144 ns |  0.26 |    0.01 |      - |     306 B |       43.71 |
|    AVX2 |  1000000 |    851,168.79 ns |  15,729.816 ns |  14,713.680 ns |  0.18 |    0.00 |      - |     561 B |       80.14 |
|         |          |                  |                |                |       |         |        |           |             |
| Generic | 10000000 | 47,163,430.32 ns | 848,643.005 ns | 793,821.194 ns |  1.00 |    0.00 |      - |      85 B |        1.00 |
|    SSE2 | 10000000 | 13,395,190.19 ns | 175,381.244 ns | 164,051.724 ns |  0.28 |    0.01 |      - |     319 B |        3.75 |
|    AVX2 | 10000000 |  9,475,439.94 ns | 147,895.334 ns | 151,877.619 ns |  0.20 |    0.00 |      - |     575 B |        6.76 |