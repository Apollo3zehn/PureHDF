using BenchmarkDotNet.Attributes;
using PureHDF;
using PureHDF.Selections;
using PureHDF.VOL.Native;
using System.Runtime.InteropServices;

namespace Benchmark;

// Exercises Read<Dictionary<string, object?>[]> on a 1-D contiguous dataset of
// a fixed-size compound struct with several members. This is the non-VL
// reference-memory decode path (DatatypeMessage.GetDecodeInfoForReferenceMemory
// -> the non-VariableLength else branch), where each per-element decode issues
// one ReadDatasetAsync + Seek per struct member against the live source. A
// batch of N elements over M members therefore costs N*M small driver
// dispatches on the unbatched path; the contiguous batching (IsBuffered gate on
// IH5ReadStream) collapses that into one bulk ReadDatasetAsync + N*M in-memory
// copies from a SystemMemoryStream wrapper.
//
// Reading as Dictionary<string, object?> (rather than a blittable struct) is
// what selects this path: a blittable struct of matching size takes the
// zero-copy unmanaged fast path instead, which never goes per-member and is
// unaffected by the batching change.
//
// Three access patterns, same total decode work, different per-Read batch
// sizes:
//   - ReadAll      : 1 Read call for the whole dataset (largest batch)
//   - ReadByWindow : 10 Read calls of WindowSize cells each
//   - ReadPerCell  : ElementCount Read calls of 1 cell each (batching is a
//                    no-op for batch size 1; included as a control)
//
// Measured results (net10.0, default job, 2000 elements x 6 members). 5f0a23c
// is the last release before contiguous decode batching; HEAD adds the
// IsBuffered gate on IH5ReadStream.
//
//   Method       | 5f0a23c   | HEAD      | Speedup
//   -------------|----------:|----------:|--------:
//   ReadAll      | 8.312 ms  | 1.218 ms  | 6.8x
//   ReadByWindow | 8.504 ms  | 1.233 ms  | 6.9x
//   ReadPerCell  | 11.617 ms | 6.203 ms  | 1.9x
//
// ReadAll / ReadByWindow: batching collapses N*M live driver dispatches into
// one bulk ReadDatasetAsync + N*M in-memory SystemMemoryStream copies; the ~7x
// speedup is pure dispatch reduction. Allocations drop marginally, from
// ~1.45 MB to ~1.18 MB. ReadPerCell: smaller win (batch size 1 limits bulk-read
// benefit) but per-member Seek calls are still redirected to the in-memory
// wrapper.
[MemoryDiagnoser]
public class ContiguousCompoundRead
{
    // 6 members -> 6 ReadDatasetAsync + Seek per element on the unbatched path.
    // Pack = 1 keeps the on-disk size at 44 bytes (3 doubles + int + long + float),
    // no padding, so the writer lays the compound out compactly.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Sample
    {
        public double A;
        public double B;
        public double C;
        public int D;
        public long E;
        public float F;
    }

    private const int ElementCount = 2_000;
    private const int WindowSize = 200;

    private string _filePath = default!;
    private NativeFile _file = default!;
    private IH5Dataset _dataset = default!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"purehdf-cc-bench-{Guid.NewGuid():N}.h5");

        var random = new Random(42);
        var data = new Sample[ElementCount];
        for (int i = 0; i < ElementCount; i++)
        {
            data[i] = new Sample
            {
                A = random.NextDouble(),
                B = random.NextDouble(),
                C = random.NextDouble(),
                D = random.Next(),
                E = random.NextInt64(),
                F = (float)random.NextDouble()
            };
        }

        var writeFile = new H5File();
        var declared = new H5Dataset<Sample[]>([(ulong)ElementCount]);
        writeFile["dataset"] = declared;

        using (var writer = writeFile.BeginWrite(_filePath))
            writer.Write(declared, data);

        _file = H5File.OpenRead(_filePath);
        _dataset = _file.Dataset("dataset");

        var probe = _dataset.Read<Dictionary<string, object?>[]>()!;
        if (probe.Length != ElementCount)
            throw new Exception($"setup produced {probe.Length} elements, expected {ElementCount}");
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
        var result = _dataset.Read<Dictionary<string, object?>[]>()!;
        return result.Length;
    }

    [Benchmark]
    public int ReadByWindow()
    {
        var total = 0;
        for (int start = 0; start + WindowSize <= ElementCount; start += WindowSize)
        {
            var sel = new HyperslabSelection(start: (ulong)start, block: WindowSize);
            var window = _dataset.Read<Dictionary<string, object?>[]>(
                fileSelection: sel,
                memoryDims: [WindowSize])!;
            total += window.Length;
        }
        return total;
    }

    [Benchmark]
    public int ReadPerCell()
    {
        var total = 0;
        for (int i = 0; i < ElementCount; i++)
        {
            var sel = new HyperslabSelection(start: (ulong)i, block: 1);
            var cell = _dataset.Read<Dictionary<string, object?>[]>(
                fileSelection: sel,
                memoryDims: [1UL])!;
            total += cell.Length;
        }
        return total;
    }
}
