using BenchmarkDotNet.Attributes;
using PureHDF;
using PureHDF.Selections;
using PureHDF.VOL.Native;
using System.Runtime.InteropServices;

namespace Benchmark;

// Exercises Read<T[][]> on a 1-D dataset of variable-length sequences of a
// small blittable struct, under three access patterns:
//
//   - ReadAll       : 1 Read call for the whole dataset
//   - ReadByWindow  : 10 Read calls of 60 cells each
//   - ReadPerCell   : 600 Read calls of 1 cell each
//
// Each access pattern measures the same total decode work but a different
// per-Read-call multiplier, so the relative cost of per-Read fixed overhead
// versus per-cell decode work shows up across the three rows.
//
// Measured on this machine (net10.0, default job, 600 cells x 200 elements).
// 5f0a23c is the last release before the driver read-ahead window; d972f97 adds it.
//
//   Method       |    5f0a23c |    d972f97 | Speedup
//   -------------|-----------:|-----------:|--------:
//   ReadAll      |   175.0 us |   174.4 us |   1.00x
//   ReadByWindow |   202.9 us |   197.5 us |   1.03x
//   ReadPerCell  | 1,532.0 us | 1,649.2 us |   0.93x
//
// ReadAll and ReadByWindow are flat (error bars overlap). ReadPerCell is a
// small real regression (600 single-cell Read calls each pay the window's
// per-call bounds check). This path's cost is dominated by per-cell
// global-heap decode, which the read-ahead work did not target. Allocations
// are flat (~1.4 MB ReadAll, ~2.8 MB ReadPerCell).
[MemoryDiagnoser]
public class VariableLengthCompoundRead
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Sample
    {
        public double X;
        public float Y;
    }

    private const int CellCount = 600;
    private const int ElementsPerCell = 200;
    private const int WindowSize = 60;

    private string _filePath = default!;
    private NativeFile _file = default!;
    private IH5Dataset _dataset = default!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"purehdf-vl-bench-{Guid.NewGuid():N}.h5");

        var random = new Random(42);
        var data = new Sample[CellCount][];
        for (int i = 0; i < CellCount; i++)
        {
            var arr = new Sample[ElementsPerCell];
            for (int j = 0; j < ElementsPerCell; j++)
                arr[j] = new Sample { X = random.NextDouble(), Y = (float)random.NextDouble() };
            data[i] = arr;
        }

        var writeFile = new H5File();
        var declared = new H5Dataset<Sample[][]>([(ulong)CellCount]);
        writeFile["dataset"] = declared;

        using (var writer = writeFile.BeginWrite(_filePath))
            writer.Write(declared, data);

        _file = H5File.OpenRead(_filePath);
        _dataset = _file.Dataset("dataset");

        var probe = _dataset.Read<Sample[][]>()!;
        if (probe.Length != CellCount)
            throw new Exception($"setup produced {probe.Length} cells, expected {CellCount}");

        for (int i = 0; i < CellCount; i++)
        {
            if (probe[i] is null || probe[i]!.Length != ElementsPerCell)
                throw new Exception(
                    $"cell {i} has length {probe[i]?.Length ?? -1}, expected {ElementsPerCell}");
        }
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

    [Benchmark(Baseline = true)]
    public int ReadAll()
    {
        var result = _dataset.Read<Sample[][]>()!;
        var total = 0;
        for (int i = 0; i < result.Length; i++)
            total += result[i]?.Length ?? 0;
        return total;
    }

    [Benchmark]
    public int ReadByWindow()
    {
        var total = 0;
        for (int start = 0; start + WindowSize <= CellCount; start += WindowSize)
        {
            var sel = new HyperslabSelection(start: (ulong)start, block: (ulong)WindowSize);
            var window = _dataset.Read<Sample[][]>(
                fileSelection: sel,
                memoryDims: [(ulong)WindowSize])!;
            for (int i = 0; i < window.Length; i++)
                total += window[i]?.Length ?? 0;
        }
        return total;
    }

    [Benchmark]
    public int ReadPerCell()
    {
        var total = 0;
        for (int i = 0; i < CellCount; i++)
        {
            var sel = new HyperslabSelection(start: (ulong)i, block: 1);
            var cell = _dataset.Read<Sample[][]>(
                fileSelection: sel,
                memoryDims: [1UL])!;
            total += cell[0]?.Length ?? 0;
        }
        return total;
    }
}
