The following benchmark has been performed on a DELL Latitude 5490 (Ubuntu 20.04 on Windows 10 + WSL2). The source for the benchmark is located [here](https://github.com/Apollo3zehn/HDF5.NET/tree/master/benchmarks/HDF5.NET.Benchmarks). The outcome is that [Microsoft's Deflate](https://docs.microsoft.com/de-de/dotnet/api/system.io.compression.deflatestream?view=net-5.0) implementation is a faster than the one from [SharpZipLib](https://icsharpcode.github.io/SharpZipLib/help/api/ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream.html) and that Intel's vectorized [ISA-L library](https://github.com/intel/isa-l) is much faster than both of them.

```bash
BenchmarkDotNet=v0.13.1, OS=ubuntu 20.04
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK=5.0.207
  [Host]     : .NET 5.0.10 (5.0.1021.41214), X64 RyuJIT
  DefaultJob : .NET 5.0.10 (5.0.1021.41214), X64 RyuJIT
```

## Legends
- N : Length of buffer to be uncompressed
- Mean : Arithmetic mean of all measurements
- Error : Half of 99.9% confidence interval
- StdDev : Standard deviation of all measurements
- Median : Value separating the higher half of all measurements (50th percentile)
- Ratio : Mean of the ratio distribution ([Current]/[Baseline])
- RatioSD : Standard deviation of the ratio distribution ([Current]/[Baseline])

|                 Method |        N |            Mean |         Error |        StdDev |          Median | Ratio | RatioSD |
|----------------------- |--------- |----------------:|--------------:|--------------:|----------------:|------:|--------:|
| MicrosoftDeflateStream |        1 |       857.22 ns |      6.897 ns |      5.760 ns |       857.56 ns |  1.00 |    0.00 |
|    SharpZipLibInflater |        1 |     2,034.71 ns |     32.758 ns |     30.642 ns |     2,051.62 ns |  2.37 |    0.03 |
|    Intel_ISA_L_Inflate |        1 |       621.18 ns |      2.690 ns |      2.246 ns |       621.52 ns |  0.72 |    0.00 |
|                        |          |                 |               |               |                 |       |         |
| MicrosoftDeflateStream |      100 |       921.29 ns |     18.285 ns |     21.057 ns |       914.35 ns |  1.00 |    0.00 |
|    SharpZipLibInflater |      100 |     2,281.63 ns |     44.293 ns |     71.524 ns |     2,297.53 ns |  2.47 |    0.11 |
|    Intel_ISA_L_Inflate |      100 |        61.49 ns |      0.920 ns |      0.718 ns |        61.50 ns |  0.07 |    0.00 |
|                        |          |                 |               |               |                 |       |         |
| MicrosoftDeflateStream |    10000 |     1,661.94 ns |     60.950 ns |    173.895 ns |     1,556.42 ns |  1.00 |    0.00 |
|    SharpZipLibInflater |    10000 |     3,174.51 ns |     62.940 ns |    147.119 ns |     3,128.49 ns |  1.95 |    0.15 |
|    Intel_ISA_L_Inflate |    10000 |       365.94 ns |      3.243 ns |      2.875 ns |       365.12 ns |  0.21 |    0.02 |
|                        |          |                 |               |               |                 |       |         |
| MicrosoftDeflateStream |  1000000 |    71,288.41 ns |  1,289.701 ns |  2,748.457 ns |    70,442.46 ns |  1.00 |    0.00 |
|    SharpZipLibInflater |  1000000 |   147,217.07 ns |  9,770.762 ns | 28,034.148 ns |   131,972.02 ns |  2.08 |    0.41 |
|    Intel_ISA_L_Inflate |  1000000 |    36,944.08 ns |    714.362 ns |    764.359 ns |    36,658.62 ns |  0.51 |    0.03 |
|                        |          |                 |               |               |                 |       |         |
| MicrosoftDeflateStream | 10000000 | 2,069,367.83 ns | 29,810.212 ns | 27,884.491 ns | 2,061,542.38 ns |  1.00 |    0.00 |
|    SharpZipLibInflater | 10000000 | 2,458,626.00 ns | 26,972.559 ns | 23,910.471 ns | 2,464,160.16 ns |  1.19 |    0.02 |
|    Intel_ISA_L_Inflate | 10000000 | 1,911,812.90 ns | 23,110.974 ns | 21,618.020 ns | 1,906,020.31 ns |  0.92 |    0.01 |

