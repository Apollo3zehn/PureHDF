The following benchmark has been performed on an Intel NUC Performance (Ubuntu 24.04, .NET 8). The source for the benchmark is located [here](https://github.com/Apollo3zehn/PureHDF/tree/master/benchmarks/PureHDF.Benchmarks). The outcome is that [Microsoft's Deflate](https://docs.microsoft.com/de-de/dotnet/api/system.io.compression.deflatestream?view=net-5.0) implementation is a faster than the one from [SharpZipLib](https://icsharpcode.github.io/SharpZipLib/help/api/ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream.html) and that Intel's vectorized [ISA-L library](https://github.com/intel/isa-l) is much faster than both of them.

```bash
BenchmarkDotNet v0.13.13-nightly.20240519.155, Ubuntu 24.04 LTS (Noble Numbat)
11th Gen Intel Core i7-1165G7 2.80GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.204
  [Host]     : .NET 8.0.4 (8.0.424.16909), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 8.0.4 (8.0.424.16909), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

## Legends
- N : Length of buffer to be uncompressed
- Mean : Arithmetic mean of all measurements
- Error : Half of 99.9% confidence interval
- StdDev : Standard deviation of all measurements
- Median : Value separating the higher half of all measurements (50th percentile)
- Ratio : Mean of the ratio distribution ([Current]/[Baseline])
- RatioSD : Standard deviation of the ratio distribution ([Current]/[Baseline])

| Method                 | N        | Mean            | Error         | StdDev         | Median          | Ratio | RatioSD |
|----------------------- |--------- |----------------:|--------------:|---------------:|----------------:|------:|--------:|
| MicrosoftDeflateStream | 1        |       580.53 ns |     11.483 ns |      15.718 ns |       579.17 ns |  1.00 |    0.04 |
| SharpZipLibInflater    | 1        |     1,357.13 ns |     26.802 ns |      44.037 ns |     1,344.15 ns |  2.34 |    0.10 |
| Intel_ISA_L_Inflate    | 1        |       167.25 ns |      2.341 ns |       2.189 ns |       166.66 ns |  0.29 |    0.01 |
|                        |          |                 |               |                |                 |       |         |
| MicrosoftDeflateStream | 100      |       594.76 ns |     11.553 ns |      16.569 ns |       583.83 ns |  1.00 |    0.04 |
| SharpZipLibInflater    | 100      |     1,408.39 ns |      6.153 ns |       5.138 ns |     1,409.45 ns |  2.37 |    0.06 |
| Intel_ISA_L_Inflate    | 100      |        47.62 ns |      0.733 ns |       0.612 ns |        47.33 ns |  0.08 |    0.00 |
|                        |          |                 |               |                |                 |       |         |
| MicrosoftDeflateStream | 10000    |       881.69 ns |     17.455 ns |      16.327 ns |       883.98 ns |  1.00 |    0.03 |
| SharpZipLibInflater    | 10000    |     2,188.97 ns |     43.687 ns |      97.712 ns |     2,149.59 ns |  2.48 |    0.12 |
| Intel_ISA_L_Inflate    | 10000    |       166.30 ns |      2.897 ns |       2.568 ns |       165.18 ns |  0.19 |    0.00 |
|                        |          |                 |               |                |                 |       |         |
| MicrosoftDeflateStream | 1000000  |    66,957.84 ns |  1,280.329 ns |   1,197.621 ns |    67,591.70 ns |  1.00 |    0.02 |
| SharpZipLibInflater    | 1000000  |   106,301.38 ns |  2,103.651 ns |   3,572.159 ns |   105,601.44 ns |  1.59 |    0.06 |
| Intel_ISA_L_Inflate    | 1000000  |    47,309.67 ns |  1,536.951 ns |   4,458.973 ns |    45,740.13 ns |  0.71 |    0.07 |
|                        |          |                 |               |                |                 |       |         |
| MicrosoftDeflateStream | 10000000 |   892,349.22 ns | 35,053.461 ns | 100,574.957 ns |   850,217.88 ns |  1.01 |    0.15 |
| SharpZipLibInflater    | 10000000 | 1,721,960.64 ns | 50,380.408 ns | 148,547.780 ns | 1,696,493.19 ns |  1.95 |    0.26 |
| Intel_ISA_L_Inflate    | 10000000 |   842,580.82 ns |  9,966.139 ns |   9,322.332 ns |   841,543.17 ns |  0.95 |    0.10 |