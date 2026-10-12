using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
///     Reading a fixed-length string costs what the value needs, not what its declared width does.
/// </summary>
/// <remarks>
///     A wide declared width is cheap on disk once compressed, since padding deflates to almost nothing, so
///     a reader must not undo that by materialising the padding. Turning the whole field into a string,
///     padding included, and then splitting it at the terminator costs two strings the width of the field
///     for every element: for 100,000 elements of 8192 bytes, 3.1 GB of garbage to return 5 MB of text.
/// </remarks>
public class FixedLengthStringReadCostTests
{
    private const int Width = 8192;
    private const int Rows = 200;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadingAWideColumnDoesNotMaterialiseItsPadding(bool chunked)
    {
        // Arrange
        var data = Enumerable.Range(0, Rows).Select(i => $"row-{i}").ToArray();

        var file = new H5File
        {
            ["column"] = new H5Dataset<string[]>(data, chunks: chunked ? [Rows / 4] : null)
        };

        var stream = new MemoryStream();
        file.Write(stream, new H5WriteOptions(DefaultStringLength: Width));
        stream.Position = 0;

        using var readFile = H5File.Open(stream);
        var dataset = readFile.Dataset("column");

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var actual = dataset.Read<string[]>();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(data, actual);

        // One UTF-16 string as wide as the field is 2 * Width bytes. The buffers the raw bytes pass
        // through are allowed for - the whole column is Rows * Width - but not a field-wide string per row.
        Assert.True(
            allocated < (long)Rows * Width * 2,
            $"Reading allocated {allocated:N0} bytes, at least one field-wide string per row.");
    }
}
