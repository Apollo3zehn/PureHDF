using Xunit;

namespace PureHDF.Tests.Writing;

public partial class DatasetTests
{
    [Fact]
    public void CanWrite_Compact()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["compact"] = data
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_compact.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);

            using var h5File = H5File.OpenRead(filePath);
            Assert.Equal(H5DataLayoutClass.Compact, h5File.Dataset("compact").Layout.Class);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_Compact_Deferred()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["compact"] = new H5Dataset<int[]>(dimensions: new ulong[] { 10 })
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_compact.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);

            using var h5File = H5File.OpenRead(filePath);
            Assert.Equal(H5DataLayoutClass.Compact, h5File.Dataset("compact").Layout.Class);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}