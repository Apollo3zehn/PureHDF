using PureHDF.Filters;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     Writing never reads from the stream it writes to.
/// </summary>
/// <remarks>
///     Reading a checksummed structure back to checksum it would cost a read per structure, and it rules
///     out a destination that cannot serve reads of what it has been given - such as an upload that keeps
///     only the part it is filling. Checksums are computed from the encoded bytes, so the stream need not
///     be readable at all. Each case here takes a path that writes a checksummed structure.
/// </remarks>
public class WriteWithoutReadingTests
{
    public static TheoryData<H5MetadataPlacement> Placements => new()
    {
        H5MetadataPlacement.FrontLoaded,
        H5MetadataPlacement.Aggregated,
        H5MetadataPlacement.Interleaved
    };

    [SkippableTheory]
    [MemberData(nameof(Placements))]
    public void ADeferredContiguousDatasetIsWrittenWithoutReading(H5MetadataPlacement placement)
    {
        // Arrange
        var data = SharedTestData.SmallData;
        var dataset = new H5Dataset<int[]>(fileDims: [(ulong)data.Length]);

        var file = new H5File
        {
            ["group"] = new H5Group { ["contiguous"] = dataset }
        };

        // Act
        var bytes = WriteForbiddingReads(file, new H5WriteOptions { MetadataPlacement = placement }, writer =>
            writer.Write(dataset, data));

        // Assert
        AssertValidAndEqual(bytes, "/group/contiguous", data);
    }

    // The single-chunk index patches its dataset's object header once the filtered chunk's size is known.
    [SkippableTheory]
    [MemberData(nameof(Placements))]
    public void ASingleFilteredChunkIsWrittenWithoutReading(H5MetadataPlacement placement)
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset<int[]>(
            fileDims: [(ulong)data.Length],
            chunks: [(uint)data.Length]);

        var file = new H5File { ["chunked"] = dataset };

        var options = new H5WriteOptions(Filters: [DeflateFilter.Id]) { MetadataPlacement = placement };

        // Act
        var bytes = WriteForbiddingReads(file, options, writer => writer.Write(dataset, data));

        // Assert
        AssertValidAndEqual(bytes, "/chunked", data);
    }

    // Several filtered chunks are indexed by a fixed array, whose header and data block are checksummed.
    [SkippableTheory]
    [MemberData(nameof(Placements))]
    public void SeveralFilteredChunksAreWrittenWithoutReading(H5MetadataPlacement placement)
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset<int[]>(
            fileDims: [(ulong)data.Length],
            chunks: [(uint)data.Length / 4]);

        var file = new H5File { ["chunked"] = dataset };

        var options = new H5WriteOptions(Filters: [DeflateFilter.Id]) { MetadataPlacement = placement };

        // Act
        var bytes = WriteForbiddingReads(file, options, writer => writer.Write(dataset, data));

        // Assert
        AssertValidAndEqual(bytes, "/chunked", data);
    }

    // Variable-length strings go to the global heap, and an attribute's value is encoded into its header.
    [SkippableTheory]
    [MemberData(nameof(Placements))]
    public void VariableLengthStringsAndAttributesAreWrittenWithoutReading(H5MetadataPlacement placement)
    {
        // Arrange
        var data = new[] { "one", "two", "three" };

        var file = new H5File
        {
            ["strings"] = new H5Dataset(data)
            {
                Attributes = new() { ["unit"] = "volt" }
            }
        };

        // Act
        var bytes = WriteForbiddingReads(file, new H5WriteOptions { MetadataPlacement = placement }, _ => { });

        // Assert
        AssertValidAndEqual(bytes, "/strings", data);

        using var actual = H5File.Open(new MemoryStream(bytes));
        Assert.Equal("volt", actual.Dataset("strings").Attribute("unit").Read<string>());
    }

    private static byte[] WriteForbiddingReads(H5File file, H5WriteOptions options, Action<H5NativeWriter> writeData)
    {
        using var stream = new WriteOnlyStream();

        using (var writer = file.BeginWrite(stream, options))
        {
            writeData(writer);
        }

        return stream.ToArray();
    }

    private static void AssertValidAndEqual<T>(byte[] bytes, string path, T[] expected)
    {
        // h5dump verifies every checksum, so a checksum computed from the wrong bytes fails here.
        var filePath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(filePath, bytes);

            var dump = TestUtils.RunH5Dump(filePath);
            Skip.If(dump is null, "h5dump is not installed.");

            Assert.False(dump.Failed, dump.Diagnostics);
        }

        finally
        {
            File.Delete(filePath);
        }

        using var actual = H5File.Open(new MemoryStream(bytes));
        Assert.Equal(expected, actual.Dataset(path).Read<T[]>());
    }

    /// <summary>
    ///     Reports itself not readable, and fails any read in case the writer reads regardless.
    /// </summary>
    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;

        public override int Read(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("The writer read from the stream it writes to.");

        public override int Read(Span<byte> buffer)
            => throw new InvalidOperationException("The writer read from the stream it writes to.");

        public override int ReadByte()
            => throw new InvalidOperationException("The writer read from the stream it writes to.");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The writer read from the stream it writes to.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The writer read from the stream it writes to.");
    }
}
