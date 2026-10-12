using PureHDF.Filters;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     A front-loaded write encodes its structure once and reserves exactly what it needs.
/// </summary>
/// <remarks>
///     Payload is placed after the structure, so every payload address depends on how large the structure
///     turns out to be. Rather than encode the tree twice - once to measure the structure, once for real -
///     the writer places payload provisionally, holds the structure in memory until its size is known, and
///     moves the payload addresses it contains into place before writing it.
/// </remarks>
public class SinglePassFrontLoadingTests
{
    public static TheoryData<string, Func<H5File>, bool> ShapesWithAndWithoutCompactLayout()
    {
        var data = new TheoryData<string, Func<H5File>, bool>();

        foreach (var shape in MetadataLayoutTests.Shapes)
        {
            data.Add((string)shape[0], (Func<H5File>)shape[1], false);
            data.Add((string)shape[0], (Func<H5File>)shape[1], true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShapesWithAndWithoutCompactLayout))]
    public void EveryShapeFrontLoadsIntoOneExactRegion(string label, Func<H5File> makeFile, bool preferCompact)
    {
        // Arrange
        var interleaved = new MemoryStream();
        makeFile().Write(interleaved, new H5WriteOptions(PreferCompactDatasetLayout: preferCompact) { MetadataPlacement = H5MetadataPlacement.Interleaved });

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            var writer = makeFile().BeginWrite(filePath, new H5WriteOptions(PreferCompactDatasetLayout: preferCompact) { MetadataPlacement = H5MetadataPlacement.FrontLoaded });
            writer.Dispose();

            // Assert - one region, used to the last byte, so the file is larger than an interleaved one only
            // by what is left of the last block of variable-length payload
            var freeSpace = writer.Context.FreeSpaceManager;

            Assert.True(1 == freeSpace.MetadataRegionsOpened, $"{label}: {freeSpace.MetadataRegionsOpened} regions opened.");
            Assert.Equal(0, freeSpace.MetadataAbandoned);
            Assert.Equal(interleaved.Length + freeSpace.PayloadAbandoned, new FileInfo(filePath).Length);

            TestUtils.DumpH5File(filePath);
            AssertSameContent(interleaved, filePath);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void DeferredWritesStayInsideTheReservation()
    {
        // Arrange - every kind of dataset whose structure is completed by a later write
        var ints = Enumerable.Range(0, 1 << 16).ToArray();
        var strings = Enumerable.Range(0, 2_000).Select(i => new string('x', 120) + i).ToArray();

        var filtered = new H5Dataset<int[]>(fileDims: [(ulong)ints.Length], chunks: [4096], datasetCreation: new(Filters: [DeflateFilter.Id]));
        var filteredSingleChunk = new H5Dataset<int[]>(fileDims: [(ulong)ints.Length], chunks: [(uint)ints.Length], datasetCreation: new(Filters: [DeflateFilter.Id]));
        var unfiltered = new H5Dataset<int[]>(fileDims: [(ulong)ints.Length], chunks: [4096]);
        var contiguous = new H5Dataset<int[]>(fileDims: [(ulong)ints.Length]);
        var variableLength = new H5Dataset<string[]>(fileDims: [(ulong)strings.Length]);

        var file = new H5File
        {
            ["filtered"] = filtered,
            ["filtered-single-chunk"] = filteredSingleChunk,
            ["unfiltered"] = unfiltered,
            ["contiguous"] = contiguous,
            ["variable-length"] = variableLength
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            var writer = file.BeginWrite(filePath, new H5WriteOptions { MetadataPlacement = H5MetadataPlacement.FrontLoaded });

            writer.Write(filtered, ints);
            writer.Write(filteredSingleChunk, ints);
            writer.Write(unfiltered, ints);
            writer.Write(contiguous, ints);
            writer.Write(variableLength, strings);
            writer.Dispose();

            // Assert
            var freeSpace = writer.Context.FreeSpaceManager;

            Assert.Equal(1, freeSpace.MetadataRegionsOpened);
            Assert.Equal(0, freeSpace.MetadataAbandoned);

            TestUtils.DumpH5File(filePath);

            using var actual = H5File.OpenRead(filePath);

            Assert.Equal(ints, actual.Dataset("filtered").Read<int[]>());
            Assert.Equal(ints, actual.Dataset("filtered-single-chunk").Read<int[]>());
            Assert.Equal(ints, actual.Dataset("unfiltered").Read<int[]>());
            Assert.Equal(ints, actual.Dataset("contiguous").Read<int[]>());
            Assert.Equal(strings, actual.Dataset("variable-length").Read<string[]>());
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void BeginWriteWritesTheStructureFrontToBack()
    {
        // Arrange - attribute values live in global heap collections, which must not be written after the
        // headers that point at them, back inside the region
        var file = new H5File();

        for (var i = 0; i < 200; i++)
        {
            var dataset = new H5Dataset<double[]>(fileDims: [10]);
            dataset.Attributes["unit"] = $"volt-{i}";
            file[$"d{i}"] = dataset;
        }

        var stream = new RecordingStream();

        // Act
        using var writer = file.BeginWrite(stream, new H5WriteOptions
        {
            AttributeStringLength = H5AttributeStringLength.VariableLength,
            MetadataPlacement = H5MetadataPlacement.FrontLoaded
        });
        var writes = stream.Writes.ToList();

        // Assert - nothing is written behind the end of an earlier write, which is what lets a streaming
        // upload ship each part as soon as it is full
        Assert.NotEmpty(writes);

        for (var i = 1; i < writes.Count; i++)
        {
            var (previousPosition, previousLength) = writes[i - 1];

            Assert.True(
                writes[i].Position >= previousPosition + previousLength,
                $"Write {i} at {writes[i].Position} lands behind write {i - 1}, which ended at {previousPosition + previousLength}.");
        }
    }

    private static void AssertSameContent(MemoryStream expected, string actualPath)
    {
        expected.Position = 0;

        using var expectedFile = H5File.Open(expected);
        using var actualFile = H5File.OpenRead(actualPath);

        foreach (var name in expectedFile.Children().Select(child => child.Name))
            Assert.True(actualFile.LinkExists(name), $"'{name}' is missing.");
    }

    /// <summary>
    ///     Records where each write lands.
    /// </summary>
    private sealed class RecordingStream : MemoryStream
    {
        public List<(long Position, long Length)> Writes { get; } = [];

        public override void Write(byte[] buffer, int offset, int count)
        {
            Writes.Add((Position, count));
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Writes.Add((Position, buffer.Length));
            base.Write(buffer);
        }
    }
}
