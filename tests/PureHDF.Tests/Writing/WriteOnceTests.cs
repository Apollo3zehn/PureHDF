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

    // An upload in parts holds a part for every gap behind the furthest write, so how many gaps there are
    // at once must not grow with the file. Payload given with the tree lands between metadata blocks, and
    // small blocks are replaced over and over while the structure is encoded.
    [Fact]
    public void TheTailsOfReplacedBlocksAreFilledWhileTheStructureIsEncoded()
    {
        // Arrange
        var file = new H5File();

        for (var i = 0; i < 1_000; i++)
            file[$"dataset_{i:D4}"] = new H5Dataset(RandomDoubles(i, 1_000));

        var options = new H5WriteOptions
        {
            MetadataPlacement = H5MetadataPlacement.Aggregated,
            MetadataBlockSize = 64 * 1024
        };

        using var stream = new CoverageRecordingStream();

        // Act
        using (file.BeginWrite(stream, options))
        {
        }

        // Assert - the superblock, written last, and the metadata block still being filled
        Assert.True(stream.MostHoles <= 2, $"{stream.MostHoles} gaps were open at once.");
    }

    // A filtered dataset's chunk index can be written as soon as its last chunk is. Given with the tree,
    // that is before its header exists, and an index left until the end would leave a gap per dataset.
    [Fact]
    public void TheIndexOfAFilteredDatasetGivenWithTheTreeIsWrittenWithIt()
    {
        // Act
        var few = MostHolesWritingFilteredDatasets(count: 10);
        var many = MostHolesWritingFilteredDatasets(count: 1_000);

        // Assert - the superblock, written last, the metadata block still being filled, and the index
        // of the dataset whose chunks are being written
        Assert.Equal(few, many);
        Assert.True(many <= 3, $"{many} gaps were open at once.");
    }

    // With AllocatePayloadOnFirstWrite, a deferred dataset's payload is allocated on its first write, at the
    // end of the file in the order the datasets are written. Allocated when declared, every dataset written
    // out of declaration order would leave the ones still to be written as payload-sized gaps behind it, and
    // an upload would hold nearly the whole file.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredDatasetsWrittenInAnyOrderLeaveNoPayloadBehind(bool chunked)
    {
        var holeBytes = MostHoleBytesWritingOutOfOrder(chunked, allocatePayloadOnFirstWrite: true);

        // Headers still to be written and the open metadata block, not 80 kB of payload each
        Assert.True(holeBytes <= 256 * 1024, $"{holeBytes} bytes were unwritten behind the furthest write.");
    }

    // By default the payload is allocated when the dataset is declared, so that no header has to be held
    // until its dataset is written: on a destination that can be written anywhere, the gaps cost nothing.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ByDefaultDeferredPayloadIsAllocatedWhenDeclared(bool chunked)
    {
        var holeBytes = MostHoleBytesWritingOutOfOrder(chunked, allocatePayloadOnFirstWrite: false);

        Assert.True(holeBytes > 1024 * 1024, $"only {holeBytes} bytes were unwritten behind the furthest write.");
    }

    private static long MostHoleBytesWritingOutOfOrder(bool chunked, bool allocatePayloadOnFirstWrite)
    {
        // Arrange
        const int count = 100;
        const int length = 10_000;

        var file = new H5File();
        var datasets = new H5Dataset<double[]>[count];

        for (var i = 0; i < count; i++)
        {
            datasets[i] = new H5Dataset<double[]>(fileDims: [length], chunks: chunked ? [length / 4] : null);
            file[$"dataset_{i:D3}"] = datasets[i];
        }

        var order = Enumerable.Range(0, count).ToArray();
        new Random(0).Shuffle(order);

        var options = new H5WriteOptions
        {
            MetadataPlacement = H5MetadataPlacement.Aggregated,
            MetadataBlockSize = 64 * 1024,
            AllocatePayloadOnFirstWrite = allocatePayloadOnFirstWrite
        };

        using var stream = new CoverageRecordingStream();

        using (var writer = file.BeginWrite(stream, options))
        {
            foreach (var i in order)
                writer.Write(datasets[i], RandomDoubles(i, length));
        }

        using var actual = H5File.Open(new MemoryStream(stream.ToArray()));

        for (var i = 0; i < count; i++)
            Assert.Equal(RandomDoubles(i, length), actual.Dataset($"dataset_{i:D3}").Read<double[]>());

        return stream.MostHoleBytes;
    }

    [Fact]
    public void AStreamingWriteWritesEveryByteOnce()
    {
        // Arrange
        var options = new H5WriteOptions { MetadataBlockSize = 64 * 1024 };
        var stream = new CoverageRecordingStream();
        var deferred = new List<(H5Dataset<double[]> Dataset, double[] Data)>();
        List<(long Address, long Length)> openTails;

        // Act
        using (var writer = H5File.BeginStreaming(stream, options))
        {
            writer.Root.Attributes["title"] = "streamed";

            for (var i = 0; i < 300; i++)
            {
                var direct = new H5Dataset<double[]>(fileDims: [500], chunks: i % 3 == 0 ? [100] : null);
                var nested = new H5Dataset<double[]>(fileDims: [500], chunks: i % 2 == 0 ? [500] : null, datasetCreation: i % 4 == 0 ? Deflate : default);

                deferred.Add((direct, RandomDoubles(2 * i, 500)));
                deferred.Add((nested, RandomDoubles(2 * i + 1, 500)));

                writer.Append($"batch_{i / 50}/direct_{i}", direct);
                writer.Append($"batch_{i / 50}/document_{i}", new H5Group
                {
                    Attributes = new() { ["index"] = i },
                    ["strings"] = RandomStrings(i, 20),
                    ["values"] = RandomDoubles(i, 100),
                    ["nested"] = nested
                });

                if (i % 50 == 49)
                    writer.Close($"batch_{i / 50}");
            }

            var order = Enumerable.Range(0, deferred.Count).ToArray();
            new Random(0).Shuffle(order);

            foreach (var i in order)
                writer.Write(deferred[i].Dataset, deferred[i].Data);

            openTails = writer.NativeWriter.Context.FreeSpaceManager.OpenTails.ToList();
        }

        // Assert
        var rewritten = stream.Ranges(count => count > 1);
        Assert.True(rewritten.Count == 0, $"Rewritten (offset, length): {Describe(rewritten)}");

        var unexpected = stream
            .Ranges(count => count == 0)
            .Where(hole => !openTails.Any(tail => tail.Address <= hole.Offset && hole.Offset + hole.Length <= tail.Address + tail.Length))
            .ToList();

        Assert.True(unexpected.Count == 0, $"Unwritten (offset, length): {Describe(unexpected)}");

        using var file = H5File.Open(new MemoryStream(stream.ToArray()));

        Assert.Equal(deferred[598].Data, file.Dataset("batch_5/direct_299").Read<double[]>());
        Assert.Equal(deferred[599].Data, file.Dataset("batch_5/document_299/nested").Read<double[]>());
    }

    // A deferred dataset appended directly into an open group is named by nothing written until its group
    // closes, so its header is not allocated until it is written: datasets appended long before they are
    // written leave no gap for their headers. Inside an appended group, the group's header names the
    // dataset's, which has to be allocated then and stays a gap until the dataset is written.
    [Fact]
    public void DatasetsAppendedDirectlyLeaveNoGapUntilWritten()
    {
        // Act
        var direct = MostHoleBytesStreamingDatasetsWrittenLate(nested: false, count: 4_000);
        var nested = MostHoleBytesStreamingDatasetsWrittenLate(nested: true, count: 4_000);

        // Assert - the open metadata block and a batch of completed headers, not a header per dataset
        Assert.True(direct <= 160 * 1024, $"{direct} bytes were unwritten behind the furthest write.");
        Assert.True(nested > 3 * direct, $"Nested: {nested} bytes, direct: {direct} bytes.");
    }

    // A late header that does not fit what is left of the metadata block replaces the block, on a write.
    // The tail left behind has to be filled then rather than at the end, or it holds its part until then.
    [Fact]
    public void TheTailsOfBlocksReplacedByLateHeadersAreFilledAsTheyAreReplaced()
    {
        // Arrange
        const int count = 2_000;

        var options = new H5WriteOptions { MetadataBlockSize = 64 * 1024 };
        var stream = new CoverageRecordingStream();
        var datasets = new H5Dataset<double[]>[count];

        // Act
        using (var writer = H5File.BeginStreaming(stream, options))
        {
            for (var i = 0; i < count; i++)
            {
                datasets[i] = new H5Dataset<double[]>(fileDims: [1_000]);
                writer.Append($"documents/{i}", datasets[i]);
            }

            for (var i = 0; i < count; i++)
                writer.Write(datasets[i], RandomDoubles(i, 1_000));
        }

        // Assert - the superblock, written last, the open metadata block and a batch of completed headers
        Assert.True(stream.MostHoles <= 3, $"{stream.MostHoles} gaps were open at once.");
    }

    private static long MostHoleBytesStreamingDatasetsWrittenLate(bool nested, int count)
    {
        var options = new H5WriteOptions { MetadataBlockSize = 64 * 1024 };
        var stream = new CoverageRecordingStream();
        var datasets = new H5Dataset<double[]>[count];

        using (var writer = H5File.BeginStreaming(stream, options))
        {
            for (var i = 0; i < count; i++)
            {
                datasets[i] = new H5Dataset<double[]>(fileDims: [1_000]);

                if (nested)
                    writer.Append($"documents/{i}", new H5Group { ["data"] = datasets[i] });

                else
                    writer.Append($"documents/{i}", datasets[i]);

                // Given data between the declaration and the write, which the gaps sit behind.
                writer.Append($"given/{i}", new H5Dataset(RandomDoubles(i, 1_000)));
            }

            for (var i = 0; i < count; i++)
                writer.Write(datasets[i], RandomDoubles(i, 1_000));
        }

        using var file = H5File.Open(new MemoryStream(stream.ToArray()));
        var path = nested ? $"documents/{count - 1}/data" : $"documents/{count - 1}";
        Assert.Equal(RandomDoubles(count - 1, 1_000), file.Dataset(path).Read<double[]>());

        return stream.MostHoleBytes;
    }

    private static int MostHolesWritingFilteredDatasets(int count)
    {
        var file = new H5File();

        for (var i = 0; i < count; i++)
            file[$"dataset_{i:D4}"] = new H5Dataset(RandomDoubles(i, 1_000), chunks: [250]);

        var options = new H5WriteOptions(Filters: [DeflateFilter.Id])
        {
            MetadataPlacement = H5MetadataPlacement.Aggregated
        };

        using var stream = new CoverageRecordingStream();

        using (file.BeginWrite(stream, options))
        {
        }

        using var actual = H5File.Open(new MemoryStream(stream.ToArray()));
        Assert.Equal(RandomDoubles(count - 1, 1_000), actual.Dataset($"dataset_{count - 1:D4}").Read<double[]>());

        return stream.MostHoles;
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

        // Everything written so far, as disjoint ranges; the gaps between them are the holes.
        private readonly List<(long Start, long End)> _touched = [];

        /// <summary>The most gaps behind the furthest write at any one time.</summary>
        public int MostHoles { get; private set; }

        /// <summary>The most bytes in those gaps at any one time.</summary>
        public long MostHoleBytes { get; private set; }

        private long _touchedBytes;

        private void Record(long position, int count)
        {
            var end = position + count;

            Touch(position, end);

            if (end > _counts.Length)
                Array.Resize(ref _counts, (int)Math.Max(end, _counts.Length * 2L));

            for (var i = position; i < end; i++)
            {
                if (_counts[i] < byte.MaxValue)
                    _counts[i]++;
            }
        }

        private void Touch(long start, long end)
        {
            var index = _touched.FindIndex(range => range.End >= start);

            if (index < 0)
                index = _touched.Count;

            while (index < _touched.Count && _touched[index].Start <= end)
            {
                start = Math.Min(start, _touched[index].Start);
                end = Math.Max(end, _touched[index].End);
                _touchedBytes -= _touched[index].End - _touched[index].Start;
                _touched.RemoveAt(index);
            }

            _touched.Insert(index, (start, end));
            _touchedBytes += end - start;

            var holes = _touched.Count - 1 + (_touched[0].Start > 0 ? 1 : 0);
            MostHoles = Math.Max(MostHoles, holes);
            MostHoleBytes = Math.Max(MostHoleBytes, _touched[^1].End - _touchedBytes);
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
