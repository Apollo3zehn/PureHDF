using Xunit;

namespace PureHDF.Tests.RoundTrip;

public class UserBlockRoundTripTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(512UL)]
    [InlineData(1024UL)]
    [InlineData(2048UL)]
    [InlineData(4096UL)]
    public void OpensFileWithSuperblockAtSupportedOffset(ulong userBlockSize)
    {
        // Arrange
        var expected = Enumerable.Range(0, 4096).ToArray();

        var file = new H5File
        {
            ["d"] = expected
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath, new H5WriteOptions { UserBlockSize = userBlockSize });

        // Assert
        try
        {
            using var actualFile = H5File.OpenRead(filePath);
            var actual = actualFile.Dataset("d").Read<int[]>();

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
