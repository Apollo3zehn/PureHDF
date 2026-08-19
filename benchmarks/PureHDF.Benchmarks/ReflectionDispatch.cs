using BenchmarkDotNet.Attributes;
using PureHDF;
using System.Runtime.InteropServices;

namespace Benchmark;

// Isolates the per-call dispatch cost on the three sites that cache reflection delegates:
//
//   1. NativeAttribute.Read<T>             — (TResult, TElement) reader delegate cache
//   2. NativeDataset.Read<T>               — same pattern
//   3. DatatypeMessage.GetDecodeInfo<T>    — closure-tree cache, plus the inner
//      GetDecodeInfoForUnmanagedElement(Type) per-Type delegate cache used while
//      building the compound decoder.
//
// The payload on each Read is intentionally tiny (one scalar or one small
// blittable compound), so per-call cost is dominated by the dispatch path
// being measured rather than by the actual decode work. A high iteration
// count inside each [Benchmark] method amplifies the per-call signal.
//
// Compound variants additionally exercise the static
// GetDecodeInfoForUnmanagedElement(Type) cache because the compound branch
// of BuildDecodeInfo routes the known-compound case through the Type-keyed
// overload (DatatypeMessage.Reading.cs:438).
//
// Measured on this machine (net10.0, default job, 10,000 Read calls per
// method). 5f0a23c is the last release before the driver read-ahead window;
// d972f97 adds it.
//
//   Method                       |  5f0a23c |  d972f97 | Speedup
//   -----------------------------|---------:|---------:|--------:
//   Dataset_ReadScalarInt        | 5.300 ms | 6.762 ms |   0.78x
//   Dataset_ReadScalarCompound   | 7.422 ms | 8.386 ms |   0.89x
//   Attribute_ReadScalarInt      | 1.199 ms | 1.452 ms |   0.83x
//   Attribute_ReadScalarCompound | 2.108 ms | 2.543 ms |   0.83x
//
// All four regress: 13-28% slower per call with tight error bars (under 3% of
// the mean at the default job). These paths issue tiny reads that get no
// coalescing benefit and pay the read-ahead window's per-call bounds check on
// every Read; the per-call overhead is the honest cost of the window on the
// tiny-payload path. Allocations are unchanged. Kept as the guard that this
// overhead does not grow further.
[MemoryDiagnoser]
public class ReflectionDispatch
{
    // Pack = 1 keeps the on-disk size predictable (12 B: double + float, no
    // trailing pad). Matches the shape used by VariableLengthCompoundRead on
    // the perf branch so numbers are comparable.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Sample
    {
        public double X;
        public float Y;
    }

    private const int Iterations = 10_000;

    private string _filePath = default!;
    private IDisposable _file = default!;
    private IH5Dataset _scalarIntDataset = default!;
    private IH5Dataset _scalarSampleDataset = default!;
    private IH5Attribute _scalarIntAttribute = default!;
    private IH5Attribute _scalarSampleAttribute = default!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _filePath = Path.Combine(
            Path.GetTempPath(),
            $"purehdf-reflection-bench-{Guid.NewGuid():N}.h5");

        var writeFile = new H5File
        {
            ["scalar_int"] = new H5Dataset(data: 42),
            ["scalar_sample"] = new H5Dataset(data: new Sample { X = 1.5, Y = 2.5f })
        };

        writeFile.Attributes["scalar_int"] = 42;
        writeFile.Attributes["scalar_sample"] = new Sample { X = 1.5, Y = 2.5f };

        writeFile.Write(_filePath);

        var root = H5File.OpenRead(_filePath);
        _file = root;

        _scalarIntDataset = root.Dataset("scalar_int");
        _scalarSampleDataset = root.Dataset("scalar_sample");
        _scalarIntAttribute = root.Attribute("scalar_int");
        _scalarSampleAttribute = root.Attribute("scalar_sample");

        // Warm the per-instance / per-Type caches so the measured loop is
        // steady-state cache-hit behaviour, not cold-build.
        _ = _scalarIntDataset.Read<int>();
        _ = _scalarSampleDataset.Read<Sample>();
        _ = _scalarIntAttribute.Read<int>();
        _ = _scalarSampleAttribute.Read<Sample>();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _file?.Dispose();

        if (File.Exists(_filePath))
        {
            try { File.Delete(_filePath); } catch { /* ignore */ }
        }
    }

    [Benchmark]
    public int Dataset_ReadScalarInt()
    {
        var total = 0;

        for (var i = 0; i < Iterations; i++)
            total += _scalarIntDataset.Read<int>();

        return total;
    }

    [Benchmark]
    public double Dataset_ReadScalarCompound()
    {
        var total = 0.0;

        for (var i = 0; i < Iterations; i++)
            total += _scalarSampleDataset.Read<Sample>().X;

        return total;
    }

    [Benchmark]
    public int Attribute_ReadScalarInt()
    {
        var total = 0;

        for (var i = 0; i < Iterations; i++)
            total += _scalarIntAttribute.Read<int>();

        return total;
    }

    [Benchmark]
    public double Attribute_ReadScalarCompound()
    {
        var total = 0.0;

        for (var i = 0; i < Iterations; i++)
            total += _scalarSampleAttribute.Read<Sample>().X;

        return total;
    }
}
