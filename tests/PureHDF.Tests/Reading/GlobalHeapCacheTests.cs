using System.Runtime.InteropServices;
using PureHDF.Selections;
using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Covers the bound on the global heap collection cache.
/// </summary>
/// <remarks>
/// Unbounded, this is the one cache in the reader whose footprint would grow with how much data has
/// been read rather than with the shape of the file, and be released only when the file closed - it
/// holds decoded variable-length PAYLOAD, so a long-lived reader that has walked a large file would
/// retain all of it.
/// <para>
/// The bound is asserted through observable read counts rather than by measuring memory: a retained-byte
/// measurement depends on GC timing and is not reproducible, whereas "this collection is still cached
/// and that one is not" is exact. Eviction having happened at all is the thing worth proving, because a
/// bound that never engages and an unbounded cache look identical from the outside.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class GlobalHeapCacheTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Peak
    {
        public double Mz;
        public double Intensity;
    }

    private readonly ITestOutputHelper _output;

    public GlobalHeapCacheTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// A variable-length read that stays well inside the budget must still be fully cached - otherwise
    /// the eviction test below would pass for the wrong reason.
    /// </summary>
    [Fact]
    public void ASmallVariableLengthReadStaysCached()
    {
        // Arrange - 16 cells x 200 peaks x 16 bytes is ~51 KB of payload, far below the default budget.
        var (fileBytes, expected) = WriteVariableLengthFile(cellCount: 16, peaksPerCell: 200);

        using var stream = new ConcurrentStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream);
        var dataset = root.Dataset("peaks");

        // Warm up: reads every collection.
        _ = dataset.Read<Peak[][]>();

        // Act
        stream.ResetCounts();
        var actual = ReadCell(dataset, 0);

        // Assert
        Assert.Equal(0, stream.MetadataReadCount);
        AssertCellEqual(expected[0], actual);
    }

    /// <summary>
    /// A variable-length read far larger than the budget must evict: the collections read first are
    /// gone, the ones read last are still there, and everything still decodes correctly.
    /// </summary>
    /// <remarks>
    /// Sets the budget explicitly rather than leaning on the default, which is 64 MiB - large enough
    /// that provoking eviction through it would need a test file big enough to slow the suite down for
    /// no extra coverage. What is under test is the mechanism, not the default.
    /// </remarks>
    [Fact]
    public void ALargeVariableLengthReadEvictsTheOldestCollections()
    {
        // Arrange - ~2 MB of payload against a 256 KB budget, so eviction is not marginal.
        const int CellCount = 320;

        var (fileBytes, expected) = WriteVariableLengthFile(cellCount: CellCount, peaksPerCell: 400);

        var options = new H5ReadOptions(GlobalHeapCacheByteBudget: 256 * 1024);

        using var stream = new ConcurrentStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: false, options);
        var dataset = root.Dataset("peaks");

        // Warm up: reads every collection, in order, overflowing the cache many times over.
        _ = dataset.Read<Peak[][]>();

        // Act - the LAST cell read is the most recently used, so its collection must have survived.
        stream.ResetCounts();
        var lastCell = ReadCell(dataset, CellCount - 1);
        var lastCellReads = stream.MetadataReadCount;

        // ... and the FIRST is the least recently used, so its collection must be gone.
        stream.ResetCounts();
        var firstCell = ReadCell(dataset, 0);
        var firstCellReads = stream.MetadataReadCount;

        _output.WriteLine($"re-read of the most recent cell: {lastCellReads} metadata reads");
        _output.WriteLine($"re-read of the oldest cell:      {firstCellReads} metadata reads");

        // Assert
        Assert.Equal(0, lastCellReads);
        Assert.True(
            firstCellReads > 0,
            $"The oldest collection should have been evicted, but re-reading it cost {firstCellReads} metadata reads.");

        // Eviction must not corrupt anything: a re-decoded collection has to produce what the cached
        // one did.
        AssertCellEqual(expected[0], firstCell);
        AssertCellEqual(expected[CellCount - 1], lastCell);
    }

    /// <summary>
    /// Reading the whole dataset twice must produce identical data even though the second pass runs
    /// entirely against an evicting cache.
    /// </summary>
    [Fact]
    public void RepeatedLargeVariableLengthReadsStayCorrect()
    {
        // Arrange
        var (fileBytes, expected) = WriteVariableLengthFile(cellCount: 600, peaksPerCell: 400);

        using var stream = new ConcurrentStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream);
        var dataset = root.Dataset("peaks");

        // Act
        var first = dataset.Read<Peak[][]>();
        var second = dataset.Read<Peak[][]>();

        // Assert
        Assert.Equal(expected.Length, first.Length);
        Assert.Equal(expected.Length, second.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            AssertCellEqual(expected[i], [first[i]]);
            AssertCellEqual(expected[i], [second[i]]);
        }
    }

    private static Peak[][] ReadCell(IH5Dataset dataset, int index)
    {
        var selection = new HyperslabSelection(start: (ulong)index, block: 1);

        return dataset.Read<Peak[][]>(selection);
    }

    private static void AssertCellEqual(Peak[] expected, Peak[][] actual)
    {
        Assert.Single(actual);
        Assert.Equal(expected.Length, actual[0].Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Mz, actual[0][i].Mz);
            Assert.Equal(expected[i].Intensity, actual[0][i].Intensity);
        }
    }

    /// <summary>
    /// Builds a file whose only dataset is a jagged array, which PureHDF stores as variable-length
    /// sequences - i.e. in global heap collections - and returns the file bytes together with what was
    /// written.
    /// </summary>
    private static (byte[] FileBytes, Peak[][] Expected) WriteVariableLengthFile(int cellCount, int peaksPerCell)
    {
        var data = new Peak[cellCount][];
        var rng = new Random(Seed: 0);

        for (int c = 0; c < cellCount; c++)
        {
            var cell = new Peak[peaksPerCell];

            for (int i = 0; i < peaksPerCell; i++)
            {
                cell[i] = new Peak
                {
                    Mz = 100.0 + rng.NextDouble() * 900.0,
                    Intensity = rng.NextDouble() * 1_000_000.0
                };
            }

            data[c] = cell;
        }

        var filePath = Path.GetTempFileName();

        try
        {
            var file = new H5File
            {
                ["peaks"] = new H5Dataset<Peak[][]>([(ulong)cellCount])
            };

            using (var writer = file.BeginWrite(filePath))
            {
                writer.Write((H5Dataset<Peak[][]>)file["peaks"], data);
            }

            return (File.ReadAllBytes(filePath), data);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
