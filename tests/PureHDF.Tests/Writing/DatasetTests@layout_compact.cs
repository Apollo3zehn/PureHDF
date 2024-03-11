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
        var dataset = new H5Dataset<int[]>(fileDims: [(ulong)data.Length]);

        var file = new H5File
        {
            ["compact"] = dataset
        };

        var filePath = Path.GetTempFileName();

        // Act
        using (var writer = file.BeginWrite(filePath))
        {
            writer.Write(dataset, data);
        }

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_compact.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);

            using var h5File = H5File.OpenRead(filePath);

            /* special case: compact is not supported for deferred writing
             * because of the object header checksum 
             */
            Assert.Equal(H5DataLayoutClass.Contiguous, h5File.Dataset("compact").Layout.Class);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}