using BenchmarkDotNet.Attributes;
using PureHDF;
using System.Runtime.InteropServices;

namespace Benchmark;

// Isolates the per-call dispatch cost on the three sites where reflection
// caching was added:
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
