using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     How much data a compact dataset holds before it is written contiguous instead.
/// </summary>
/// <remarks>
///     The limit is the size of the layout message: an object header message declares its size in two
///     bytes, and the message carries a version, a layout class and a two-byte data size ahead of the data
///     itself. That leaves 65,531 bytes of data.
/// </remarks>
public class CompactLayoutLimitTests
{
    private const int LargestCompactData = ushort.MaxValue - 4;

    [Theory]
    [InlineData(32 * 1024, H5DataLayoutClass.Compact)]
    [InlineData(LargestCompactData, H5DataLayoutClass.Compact)]
    [InlineData(LargestCompactData + 1, H5DataLayoutClass.Contiguous)]
    public void DataUpToTheMessageSizeIsCompact(int byteCount, H5DataLayoutClass expected)
    {
        // Arrange
        var data = Enumerable.Range(0, byteCount).Select(i => (byte)i).ToArray();
        var file = new H5File { ["data"] = new H5Dataset(data) };
        var filePath = Path.GetTempFileName();

        try
        {
            // Act - the most the option allows, so that only the layout limits
            file.Write(filePath, new H5WriteOptions { MaximumCompactDatasetSize = int.MaxValue });

            // Assert
            TestUtils.DumpH5File(filePath);

            using var actual = H5File.OpenRead(filePath);
            var dataset = actual.Dataset("data");

            Assert.Equal(expected, dataset.Layout.Class);
            Assert.Equal(data, dataset.Read<byte[]>());
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
