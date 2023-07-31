using Xunit;

namespace PureHDF.Tests.Writing;

public partial class DatasetTests
{
    [Fact]
    public void CanWrite_Chunked_implicit()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(data, chunkDimensions: new[] { 10U })
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        // TODO, this test will fail until 
        // https://forum.hdfgroup.org/t/h5dump-1-14-gives-error-with-fill-value-1-12-is-ok/11385 
        // is solved
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_chunked_implicit.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ThrowsForInvalidChunkDimensionsRank()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(data, chunkDimensions: new[] { 10U, 10U })
        };

        var filePath = Path.GetTempFileName();

        // Act
        void action() => file.Save(filePath);

        // Assert
        try
        {
            Assert.Throws<Exception>(action);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ThrowsForInvalidChunkDimensions()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(data, chunkDimensions: new[] { 101U })
        };

        var filePath = Path.GetTempFileName();

        // Act
        void action() => file.Save(filePath);

        // Assert
        try
        {
            Assert.Throws<Exception>(action);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}