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
        var stream = Write(new H5File { ["strings"] = dataset }, writer => writer.Write(dataset, data));

        // Assert
        Assert.Equal(data, stream.Reopen().Dataset("strings").Read<string[]>());
        Assert.True(stream.Writes <= MaximumWrites, $"{stream.Writes} writes.");
    }

    [Fact]
    public void ADeferredContiguousDatasetWrittenInBatchesIsWrittenInFewWrites()
    {
        // Arrange
        const int batch = 2_500;

        var data = Strings();
        var dataset = new H5Dataset<string[]>(fileDims: [ElementCount]);

        // Act
        var stream = Write(new H5File { ["strings"] = dataset }, writer =>
        {
            for (var start = 0; start < ElementCount; start += batch)
            {
                writer.Write(dataset, data[start..(start + batch)],
                    fileSelection: new HyperslabSelection((ulong)start, batch));
            }
        });

        // Assert
        Assert.Equal(data, stream.Reopen().Dataset("strings").Read<string[]>());
        Assert.True(stream.Writes <= MaximumWrites, $"{stream.Writes} writes.");
    }

    [Fact]
    public void AnUnfilteredChunkedDatasetIsWrittenInFewWrites()
    {
        // Arrange - unfiltered chunks are written straight to their place in the file
        var data = Strings();
        var dataset = new H5Dataset<string[]>(fileDims: [ElementCount], chunks: [ElementCount / 4]);

        // Act
        var stream = Write(new H5File { ["strings"] = dataset }, writer => writer.Write(dataset, data));

        // Assert
        Assert.Equal(data, stream.Reopen().Dataset("strings").Read<string[]>());
        Assert.True(stream.Writes <= MaximumWrites, $"{stream.Writes} writes.");
    }

    [Fact]
    public void ADatasetGivenWithTheTreeIsWrittenInFewWrites()
    {
        // Arrange
        var data = Strings();

        // Act
        var stream = Write(new H5File { ["strings"] = new H5Dataset(data) }, _ => { });

        // Assert
        Assert.Equal(data, stream.Reopen().Dataset("strings").Read<string[]>());
        Assert.True(stream.Writes <= MaximumWrites, $"{stream.Writes} writes.");
    }

    // With AllocatePayloadOnFirstWrite, a deferred dataset's header is completed when its payload is
    // allocated, on its first write. Written
    // there one by one, each header would be a jump back from the end of the file to the metadata and each
    // payload a jump forward again, and a file stream writes out its buffer at every jump.
    [Fact]
    public void HeadersOfDeferredDatasetsAreWrittenTogether()
    {
        // Arrange
        const int count = 1_000;

        var file = new H5File();
        var datasets = new H5Dataset<double[]>[count];

        for (var i = 0; i < count; i++)
        {
            datasets[i] = new H5Dataset<double[]>(fileDims: [100]);
            file[$"dataset_{i:D4}"] = datasets[i];
        }

        // Act
        var stream = Write(file, writer =>
        {
            for (var i = 0; i < count; i++)
                writer.Write(datasets[i], Enumerable.Range(i, 100).Select(value => (double)value).ToArray());
        }, allocatePayloadOnFirstWrite: true);

        // Assert
        Assert.Equal(Enumerable.Range(count - 1, 100).Select(value => (double)value), stream.Reopen().Dataset($"dataset_{count - 1:D4}").Read<double[]>());
        Assert.True(stream.Jumps <= count / 10, $"{stream.Jumps} jumps.");
    }

    private static string[] Strings()
        => Enumerable.Range(0, ElementCount).Select(i => $"value {i}").ToArray();

    private static RecordingStream Write(H5File file, Action<H5NativeWriter> writeData, bool allocatePayloadOnFirstWrite = false)
    {
        var stream = new RecordingStream();

        var options = new H5WriteOptions
        {
            MetadataPlacement = H5MetadataPlacement.Aggregated,
            AllocatePayloadOnFirstWrite = allocatePayloadOnFirstWrite
        };

        using (var writer = file.BeginWrite(stream, options))
        {
            writeData(writer);
        }

        return stream;
    }
}
