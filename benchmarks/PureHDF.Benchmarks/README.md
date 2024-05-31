Preparation:
[Trace .NET applications with PerfCollect](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/trace-perfcollect-lttng)

Run benchmark:

```bash
sudo dotnet run \
    -c Release \
    -f net8.0 \
    --job short \
    --project benchmarks/PureHDF.Benchmarks/PureHDF.Benchmarks.csproj \
    --filter '*chunked_no_filter*' \
    --profiler perf
```

Unzip `Benchmark.chunked_no_filter.Run-20240531-102241.trace.zip` and analyze `perf.data.txt` in `https://www.speedscope.app/`.