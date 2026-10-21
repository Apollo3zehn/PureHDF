using Xunit;

namespace PureHDF.Tests.Writing;

public class UserBlockWritingTests
{
    [Fact]
    public void WritesUserBlockFileReadableByH5Dump()
    {
        // Arrange
        var file = new H5File
        {
            ["g"] = new H5Group
            {
                ["d"] = new H5Dataset(1.1, chunks: [1]),
            },
            Attributes =
            {
                ["a"] = 1
            }
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath, new H5WriteOptions { UserBlockSize = 512 });

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/misc_with_user_block.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
