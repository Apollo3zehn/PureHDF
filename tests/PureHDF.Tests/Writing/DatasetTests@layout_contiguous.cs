using Xunit;

namespace PureHDF.Tests.Writing;

public partial class DatasetTests
{
    [Fact]
    public void CanWrite_Contiguous()
    {
        // Arrange
        var data = SharedTestData.HugeData.AsMemory(0, ushort.MaxValue + 1);

        var file = new H5File
        {
            ["contiguous"] = data
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_contiguous.dump")
                .Replace("<file-path>", filePath);

            Assert.StartsWith(expected, actual);

            using var h5File = H5File.OpenRead(filePath);
            Assert.Equal(H5DataLayoutClass.Contiguous, h5File.Dataset("contiguous").Layout.Class);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_Contiguous_Prefer()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["contiguous"] = data
        };

        var filePath = Path.GetTempFileName();

        var options = new H5SerializerOptions(
            PreferCompactDatasetLayout: false
        );

        // Act
        file.Save(filePath, options);

        // Assert
        try
        {
            using var h5File = H5File.OpenRead(filePath);
            Assert.Equal(H5DataLayoutClass.Contiguous, h5File.Dataset("contiguous").Layout.Class);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}