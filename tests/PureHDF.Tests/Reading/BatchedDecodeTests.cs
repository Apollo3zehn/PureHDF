using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
///     A contiguous selection decoded element by element is read in bounded batches, and every element lands
///     where it belongs across the boundaries between them.
/// </summary>
/// <remarks>
///     Both columns are sized to need several 1 MB batches, with a count that does not divide evenly, so
///     that the last batch is a partial one.
/// </remarks>
public class BatchedDecodeTests
{
    [Fact]
    public void AFixedLengthStringColumnSpanningSeveralBatchesReadsBackWhole()
    {
        // 8192-byte elements make 128 per batch; 300 elements need three, the last one partial.
        var data = Enumerable.Range(0, 300).Select(i => $"fixed-{i}").ToArray();

        var actual = RoundTrip(data, new H5WriteOptions(DefaultStringLength: 8192));

        Assert.Equal(data, actual);
    }

    [Fact]
    public void AVariableLengthStringColumnSpanningSeveralBatchesReadsBackWhole()
    {
        // 16-byte cell headers make 65,536 per batch; 140,000 elements need three, the last one partial.
        var data = Enumerable.Range(0, 140_000).Select(i => $"variable-{i}").ToArray();

        var actual = RoundTrip(data, new H5WriteOptions(DefaultStringLength: 0));

        Assert.Equal(data, actual);
    }

    private static string[] RoundTrip(string[] data, H5WriteOptions options)
    {
        var file = new H5File { ["column"] = new H5Dataset<string[]>(data) };
        var stream = new MemoryStream();

        file.Write(stream, options);
        stream.Position = 0;

        using var readFile = H5File.Open(stream);

        return readFile.Dataset("column").Read<string[]>();
    }
}
