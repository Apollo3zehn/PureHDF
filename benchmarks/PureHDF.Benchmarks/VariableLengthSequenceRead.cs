using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using PureHDF;
using PureHDF.VOL.Native;

namespace Benchmark;

// Exercises Read<Peak[][]> on a 1-D dataset of 60 variable-length sequences of
// a blittable struct (200 peaks each). Measures the steady-state cost of the
// variable-length decode path after the global-heap cache is warm.
//
// Measured on this machine (net10.0, default job, 60 cells x 200 peaks).
// 5f0a23c is the last release before the driver read-ahead window; HEAD adds it.
//
//   Method                   | 5f0a23c  | HEAD     | Speedup
//   -------------------------|---------:|---------:|--------:
//   ReadVariableLengthPeaks  | 19.16 us | 19.65 us | 0.98x
//
// Flat (error bars overlap). This path was not targeted by any change since
// 5f0a23c; allocation is unchanged at ~192 KB.
[MemoryDiagnoser]
public class VariableLengthSequenceRead
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Peak
    {
        public double Mz;
        public double Intensity;
    }

    private const int CellCount = 60;
    private const int PeaksPerCell = 200;

    private string _filePath = null!;
    private NativeFile _file = null!;
    private IH5Dataset _dataset = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"purehdf-variable-length-bench-{Guid.NewGuid():N}.h5");

        var data = new Peak[CellCount][];
        var rng = new Random(Seed: 0);

        for (int c = 0; c < CellCount; c++)
        {
            var cell = new Peak[PeaksPerCell];

            for (int i = 0; i < PeaksPerCell; i++)
            {
                cell[i] = new Peak
                {
                    Mz = 100.0 + rng.NextDouble() * 900.0,
                    Intensity = rng.NextDouble() * 1_000_000.0
                };
            }

            data[c] = cell;
        }

        var file = new H5File
        {
            ["peaks"] = new H5Dataset<Peak[][]>([(ulong)CellCount])
        };

        using (var writer = file.BeginWrite(_filePath))
        {
            writer.Write((H5Dataset<Peak[][]>)file["peaks"], data);
        }

        _file = H5File.OpenRead(_filePath);
        _dataset = _file.Dataset("peaks");

        // warm the global-heap cache
        _ = _dataset.Read<Peak[][]>();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _file.Dispose();

        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }

    [Benchmark]
    public int ReadVariableLengthPeaks()
    {
        var actual = _dataset.Read<Peak[][]>();

        // sanity check
        var total = 0;

        for (int c = 0; c < actual.Length; c++)
        {
            total += actual[c]!.Length;
        }

        if (total != CellCount * PeaksPerCell)
            throw new Exception($"Unexpected peak count: {total}");

        return total;
    }
}
