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

        using var stream = new RecordingStream();

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

        using var stream = new RecordingStream();

        using (var writer = file.BeginWrite(stream, options))
        {
            foreach (var i in order)
                writer.Write(datasets[i], RandomDoubles(i, length));
        }

        using var actual = stream.Reopen();

        for (var i = 0; i < count; i++)
            Assert.Equal(RandomDoubles(i, length), actual.Dataset($"dataset_{i:D3}").Read<double[]>());

        return stream.MostHoleBytes;
    }

    [Fact]
    public void AStreamingWriteWritesEveryByteOnce()
    {
        // Arrange
        var options = new H5WriteOptions { MetadataBlockSize = 64 * 1024 };
        var stream = new RecordingStream();
        var deferred = new List<(H5Dataset<double[]> Dataset, double[] Data)>();
        List<(long Address, long Length)> openTails;

        // Act
        using (var writer = H5File.BeginStreaming(stream, options))
        {
            writer.Root.Attributes["title"] = "streamed";

            for (var i = 0; i < 300; i++)
            {
                var direct = new H5Dataset<double[]>(fileDims: [500], chunks: i % 3 == 0 ? [100] : null);
                var shared = new H5Dataset<double[]>(fileDims: [500], chunks: i % 2 == 0 ? [100] : null) { IsShared = true };
                var nested = new H5Dataset<double[]>(fileDims: [500], chunks: i % 2 == 0 ? [500] : null, datasetCreation: i % 4 == 0 ? Deflate : default);

                deferred.Add((direct, RandomDoubles(2 * i, 500)));
                deferred.Add((nested, RandomDoubles(2 * i + 1, 500)));

                if (i % 10 == 0)
                {
                    deferred.Add((shared, RandomDoubles(-i, 500)));
                    writer.Append($"shared_{i}", shared);
                    writer.Append($"shared_{i}_again", new H5Group { ["link"] = shared });
                }

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

        using var file = stream.Reopen();

        Assert.Equal(RandomDoubles(2 * 299, 500), file.Dataset("batch_5/direct_299").Read<double[]>());
        Assert.Equal(RandomDoubles(2 * 299 + 1, 500), file.Dataset("batch_5/document_299/nested").Read<double[]>());
        Assert.Equal(RandomDoubles(-290, 500), file.Dataset("shared_290_again/link").Read<double[]>());
    }

    // A deferred dataset's header, and a filtered one's chunk index, are complete only once its data is
    // written. Allocated before then, each is a gap behind everything written in the meantime. Appended
    // directly into an open group, a dataset is named by nothing written until the group closes; inside an
    // appended group, the group waits for it. Either way, nothing has to be allocated until it is written.
    public static TheoryData<string, bool> LateLayouts()
    {
        var cases = new TheoryData<string, bool>();

        foreach (var layout in new[] { "contiguous", "implicit", "single chunk", "deflate", "single chunk deflate" })
        {
            cases.Add(layout, false);
            cases.Add(layout, true);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(LateLayouts))]
    public void DeferredDatasetsLeaveNoGapUntilWritten(string layout, bool nested)
    {
        // Act
        var holeBytes = MostHoleBytesStreamingDatasetsWrittenLate(layout, nested, count: 4_000);

        // Assert - the open metadata block and a batch of completed headers, not a header per dataset
        Assert.True(holeBytes <= 160 * 1024, $"{holeBytes} bytes were unwritten behind the furthest write.");
    }

    // A late header that does not fit what is left of the metadata block replaces the block, on a write.
    // The tail left behind has to be filled then rather than at the end, or it holds its part until then.
    [Fact]
    public void TheTailsOfBlocksReplacedByLateHeadersAreFilledAsTheyAreReplaced()
    {
        // Arrange
        const int count = 2_000;

        var options = new H5WriteOptions { MetadataBlockSize = 64 * 1024 };
        var stream = new RecordingStream();
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

    private static long MostHoleBytesStreamingDatasetsWrittenLate(string layout, bool nested, int count)
    {
        // Nothing is shared, so the region for shared objects, a gap until the end, is kept out of the way.
        var options = new H5WriteOptions { MetadataBlockSize = 64 * 1024, SharedObjectRegionSize = 1 };
        var stream = new RecordingStream();
        var datasets = new H5Dataset<double[]>[count];

        using (var writer = H5File.BeginStreaming(stream, options))
        {
            for (var i = 0; i < count; i++)
            {
                datasets[i] = layout switch
                {
                    "contiguous" => new H5Dataset<double[]>(fileDims: [1_000]),
                    "implicit" => new H5Dataset<double[]>(fileDims: [1_000], chunks: [250]),
                    "single chunk" => new H5Dataset<double[]>(fileDims: [1_000], chunks: [1_000]),
                    "deflate" => new H5Dataset<double[]>(fileDims: [1_000], chunks: [250], datasetCreation: Deflate),
                    "single chunk deflate" => new H5Dataset<double[]>(fileDims: [1_000], chunks: [1_000], datasetCreation: Deflate),
                    _ => throw new ArgumentOutOfRangeException(nameof(layout))
                };

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

        using var file = stream.Reopen();
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

        using var stream = new RecordingStream();

        using (file.BeginWrite(stream, options))
        {
        }

        using var actual = stream.Reopen();
        Assert.Equal(RandomDoubles(count - 1, 1_000), actual.Dataset($"dataset_{count - 1:D4}").Read<double[]>());

        return stream.MostHoles;
    }

    private static (RecordingStream Stream, List<(long Address, long Length)> OpenTails) Write(
        Scenario scenario, H5MetadataPlacement placement)
    {
        var options = scenario.Options with { MetadataPlacement = placement };
        var stream = new RecordingStream();
        List<(long, long)> openTails;

        using (var writer = scenario.File.BeginWrite(stream, options))
        {
            foreach (var write in scenario.Writes)
                write(writer);

            openTails = writer.Context.FreeSpaceManager.OpenTails.ToList();
        }

        // The file is valid, and reads back.
        using var file = stream.Reopen();
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
}
