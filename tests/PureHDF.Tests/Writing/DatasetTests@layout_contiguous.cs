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
        file.Write(filePath);

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
    public void CanWrite_Contiguous_Deferred()
    {
        // Arrange
        var data = SharedTestData.HugeData.AsMemory(0, ushort.MaxValue + 1);
        var dataset = new H5Dataset<Memory<int>>(fileDims: [(ulong)data.Length]);

        var file = new H5File
        {
            ["contiguous"] = dataset
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

        var options = new H5WriteOptions(
            PreferCompactDatasetLayout: false
        );

        // Act
        file.Write(filePath, options);

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