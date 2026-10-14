using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     A dataset's payload reaches the stream in large writes, however small its elements.
/// </summary>
/// <remarks>
///     An encoder writes element by element - two writes per variable-length element, its length and its
///     heap ID, 16 bytes in all. A buffered file stream absorbs that; any other stream pays for every call,
///     20 million calls for 10 million strings if each reached it.
/// </remarks>
public class PayloadWriteBatchingTests
{
    private const int ElementCount = 10_000;

    // Far fewer than one per element: the payload is 160 kB of heap IDs, plus the structure.
    private const int MaximumWrites = 100;

    [Fact]
    public void ADeferredContiguousDatasetIsWrittenInFewWrites()
    {
        // Arrange
        var data = Strings();
        var dataset = new H5Dataset<string[]>(fileDims: [ElementCount]);

        // Act
        var (writes, file) = Write(new H5File { ["strings"] = dataset }, writer => writer.Write(dataset, data));

        // Assert
        Assert.Equal(data, file.Dataset("strings").Read<string[]>());
        Assert.True(writes <= MaximumWrites, $"{writes} writes.");
    }

    [Fact]
    public void ADeferredContiguousDatasetWrittenInBatchesIsWrittenInFewWrites()
    {
        // Arrange
        const int batch = 2_500;

        var data = Strings();
        var dataset = new H5Dataset<string[]>(fileDims: [ElementCount]);

        // Act
        var (writes, file) = Write(new H5File { ["strings"] = dataset }, writer =>
        {
            for (var start = 0; start < ElementCount; start += batch)
            {
                writer.Write(dataset, data[start..(start + batch)],
                    fileSelection: new HyperslabSelection((ulong)start, batch));
            }
        });

        // Assert
        Assert.Equal(data, file.Dataset("strings").Read<string[]>());
        Assert.True(writes <= MaximumWrites, $"{writes} writes.");
    }

    [Fact]
    public void AnUnfilteredChunkedDatasetIsWrittenInFewWrites()
    {
        // Arrange - unfiltered chunks are written straight to their place in the file
        var data = Strings();
        var dataset = new H5Dataset<string[]>(fileDims: [ElementCount], chunks: [ElementCount / 4]);

        // Act
        var (writes, file) = Write(new H5File { ["strings"] = dataset }, writer => writer.Write(dataset, data));

        // Assert
        Assert.Equal(data, file.Dataset("strings").Read<string[]>());
        Assert.True(writes <= MaximumWrites, $"{writes} writes.");
    }

    [Fact]
    public void ADatasetGivenWithTheTreeIsWrittenInFewWrites()
    {
        // Arrange
        var data = Strings();

        // Act
        var (writes, file) = Write(new H5File { ["strings"] = new H5Dataset(data) }, _ => { });

        // Assert
        Assert.Equal(data, file.Dataset("strings").Read<string[]>());
        Assert.True(writes <= MaximumWrites, $"{writes} writes.");
    }

    private static string[] Strings()
        => Enumerable.Range(0, ElementCount).Select(i => $"value {i}").ToArray();

    private static (long Writes, IH5Group File) Write(H5File file, Action<H5NativeWriter> writeData)
    {
        var stream = new WriteCountingStream();

        using (var writer = file.BeginWrite(stream, new H5WriteOptions { MetadataPlacement = H5MetadataPlacement.Aggregated }))
        {
            writeData(writer);
        }

        return (stream.Writes, H5File.Open(new MemoryStream(stream.ToArray())));
    }

    private sealed class WriteCountingStream : MemoryStream
    {
        public long Writes { get; private set; }

        // Stream.Write(ReadOnlySpan<byte>) on a derived MemoryStream routes here.
        public override void Write(byte[] buffer, int offset, int count)
        {
            Writes++;
            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            Writes++;
            base.WriteByte(value);
        }
    }
}
