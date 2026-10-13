using PureHDF.Filters;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     Every byte of a written file is written exactly once, and the only bytes not written at all are the
///     unused tails of the blocks still open when the writer is disposed.
/// </summary>
/// <remarks>
///     A destination that uploads a file in parts can ship a part as soon as every byte of it has been
///     written, provided nothing is written into it afterwards and nothing in it is left unwritten for
///     good. Rewriting a part already shipped corrupts the upload; a gap that is never filled holds its
///     part until the end. The unused tail of a block still open at the end is allocated but never named
///     by anything, so the destination can fill it with zeros once the writer is disposed.
/// </remarks>
public class WriteOnceTests
{
    private static readonly H5DatasetCreation Deflate = new(Filters: [DeflateFilter.Id]);

    private static readonly Dictionary<string, Func<Scenario>> Scenarios = new()
    {
        ["variable-length strings"] = VariableLengthStrings,
        ["many small groups"] = ManySmallGroups,
        ["large group"] = LargeGroup,
        ["deferred single chunks"] = DeferredSingleChunks,
        ["deferred single chunks never written"] = DeferredSingleChunksNeverWritten,
        ["deferred variable-length writes"] = DeferredVariableLengthWrites
    };

    public static TheoryData<string, H5MetadataPlacement> Cases()
    {
        var cases = new TheoryData<string, H5MetadataPlacement>();

        foreach (var name in Scenarios.Keys)
        {
            cases.Add(name, H5MetadataPlacement.Aggregated);
            cases.Add(name, H5MetadataPlacement.Interleaved);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void NoByteIsWrittenTwice(string scenarioName, H5MetadataPlacement placement)
    {
        // Arrange
        var scenario = Scenarios[scenarioName]();

        // Act
        var (stream, _) = Write(scenario, placement);

        // Assert
        var rewritten = stream.Ranges(count => count > 1);
        Assert.True(rewritten.Count == 0, $"Rewritten (offset, length): {Describe(rewritten)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OnlyTheTailsOfOpenBlocksAreLeftUnwritten(string scenarioName, H5MetadataPlacement placement)
    {
        // Arrange
        var scenario = Scenarios[scenarioName]();

        // Act
        var (stream, openTails) = Write(scenario, placement);

        // Assert
        var unexpected = stream
            .Ranges(count => count == 0)
            .Where(hole => !openTails.Any(tail => tail.Address <= hole.Offset && hole.Offset + hole.Length <= tail.Address + tail.Length))
            .ToList();

        Assert.True(unexpected.Count == 0, $"Unwritten (offset, length): {Describe(unexpected)}");
    }

    private static (CoverageRecordingStream Stream, List<(long Address, long Length)> OpenTails) Write(
        Scenario scenario, H5MetadataPlacement placement)
    {
        var options = scenario.Options with { MetadataPlacement = placement };
        var stream = new CoverageRecordingStream();
        List<(long, long)> openTails;

        using (var writer = scenario.File.BeginWrite(stream, options))
        {
            foreach (var write in scenario.Writes)
                write(writer);

            openTails = writer.Context.FreeSpaceManager.OpenTails.ToList();
        }

        // The file is valid, and reads back.
        using var file = H5File.Open(new MemoryStream(stream.ToArray()));
        scenario.Verify(file);

        return (stream, openTails);
    }

    private static string Describe(List<(long Offset, long Length)> ranges)
        => string.Join(", ", ranges.Take(20).Select(range => $"({range.Offset}, {range.Length})"));

    private sealed record Scenario(
        H5File File,
        Action<IH5Group> Verify,
        List<Action<H5NativeWriter>>? DeferredWrites = default,
        H5WriteOptions? WriteOptions = default)
    {
        public List<Action<H5NativeWriter>> Writes => DeferredWrites ?? [];

        public H5WriteOptions Options => WriteOptions ?? new H5WriteOptions();
    }

    #region Scenarios

    private static double[] RandomDoubles(int seed, int count)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, count).Select(_ => random.NextDouble()).ToArray();
    }

    private static string[] RandomStrings(int seed, int count)
    {
        var random = new Random(seed);

        return Enumerable.Range(0, count)
            .Select(_ => new string('a', random.Next(1, 50)))
            .ToArray();
    }

    // Heap collections, given with the tree: their reserved fields, object 0 and their free space.
    private static Scenario VariableLengthStrings()
    {
        var data = RandomStrings(1, 5_000);

        return new(
            new H5File
            {
                ["strings"] = new H5Dataset(data) { Attributes = new() { ["note"] = "a note" } }
            },
            file => Assert.Equal(data, file.Dataset("strings").Read<string[]>()));
    }

    // Shared collections of the minimum size, and metadata blocks replaced when a header does not fit.
    private static Scenario ManySmallGroups()
    {
        var file = new H5File();

        for (var i = 0; i < 2_000; i++)
        {
            file[$"group_{i:D4}"] = new H5Group
            {
                Attributes = new() { ["index"] = i },
                ["name"] = new H5Dataset($"name {i}")
            };
        }

        return new(file, actual => Assert.Equal("name 1999", actual.Dataset("group_1999/name").Read<string>()));
    }

    // A header too large for what is left of a block abandons the rest of it.
    private static Scenario LargeGroup()
    {
        var group = new H5Group();

        for (var i = 0; i < 5_000; i++)
            group[$"dataset_{i:D4}"] = new H5Dataset(i);

        return new(new H5File { ["large"] = group }, file => Assert.Equal(4_999, file.Dataset("large/dataset_4999").Read<int>()));
    }

    // A single chunk's address, and for a filtered one its size, are known only once it is written.
    private static Scenario DeferredSingleChunks()
    {
        var data = RandomDoubles(1, 2_000);
        var unfiltered = new H5Dataset<double[]>(fileDims: [2_000], chunks: [2_000]);
        var filtered = new H5Dataset<double[]>(fileDims: [2_000], chunks: [2_000], datasetCreation: Deflate);
        var written = new H5Dataset(RandomDoubles(2, 50_000));

        return new(
            new H5File { ["unfiltered"] = unfiltered, ["filtered"] = filtered, ["written"] = written },
            file =>
            {
                Assert.Equal(data, file.Dataset("unfiltered").Read<double[]>());
                Assert.Equal(data, file.Dataset("filtered").Read<double[]>());
            },
            [
                writer => writer.Write(unfiltered, data),
                writer => writer.Write(filtered, data)
            ]);
    }

    private static Scenario DeferredSingleChunksNeverWritten()
    {
        var unfiltered = new H5Dataset<double[]>(fileDims: [2_000], chunks: [2_000]);
        var filtered = new H5Dataset<double[]>(fileDims: [2_000], chunks: [2_000], datasetCreation: Deflate);

        return new(
            new H5File { ["unfiltered"] = unfiltered, ["filtered"] = filtered },
            file =>
            {
                Assert.Equal(new double[2_000], file.Dataset("unfiltered").Read<double[]>());
                Assert.Equal(new double[2_000], file.Dataset("filtered").Read<double[]>());
            });
    }

    // Collections of a dataset's own values are allocated in blocks of their own, which are replaced when
    // a collection does not fit what is left - 5,000 bytes does not divide a block evenly.
    private static Scenario DeferredVariableLengthWrites()
    {
        const int rows = 4_000;
        const int batch = 100;

        var data = RandomStrings(1, rows);
        var dataset = new H5Dataset<string[]>(fileDims: [rows]);
        var writes = new List<Action<H5NativeWriter>>();

        for (var start = 0; start < rows; start += batch)
        {
            var begin = start;

            writes.Add(writer => writer.Write(dataset, data[begin..(begin + batch)],
                fileSelection: new HyperslabSelection((ulong)begin, batch)));
        }

        return new(
            new H5File { ["strings"] = dataset },
            file => Assert.Equal(data, file.Dataset("strings").Read<string[]>()),
            writes,
            new H5WriteOptions(MinimumGlobalHeapCollectionSize: 5_000));
    }

    #endregion

    /// <summary>
    ///     Counts how often each byte is written.
    /// </summary>
    private sealed class CoverageRecordingStream : MemoryStream
    {
        private byte[] _counts = new byte[64 * 1024];

        public override bool CanRead => false;

        // Stream.Write(ReadOnlySpan<byte>) on a derived MemoryStream routes here, so this is the only
        // write path apart from WriteByte.
        public override void Write(byte[] buffer, int offset, int count)
        {
            Record(Position, count);
            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            Record(Position, 1);
            base.WriteByte(value);
        }

        private void Record(long position, int count)
        {
            var end = position + count;

            if (end > _counts.Length)
                Array.Resize(ref _counts, (int)Math.Max(end, _counts.Length * 2L));

            for (var i = position; i < end; i++)
            {
                if (_counts[i] < byte.MaxValue)
                    _counts[i]++;
            }
        }

        public List<(long Offset, long Length)> Ranges(Func<byte, bool> predicate)
        {
            var ranges = new List<(long, long)>();
            var length = Length;
            long? start = null;

            for (long i = 0; i < length; i++)
            {
                var matches = predicate(i < _counts.Length ? _counts[i] : (byte)0);

                if (matches && start is null)
                    start = i;

                else if (!matches && start is not null)
                {
                    ranges.Add((start.Value, i - start.Value));
                    start = null;
                }
            }

            if (start is not null)
                ranges.Add((start.Value, length - start.Value));

            return ranges;
        }
    }
}
