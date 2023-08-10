using Xunit;
using System.Reflection;
using PureHDF.Filters;

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
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_chunked_implicit.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);

            using var h5File = H5File.OpenRead(filePath);
            Assert.Equal(H5DataLayoutClass.Chunked, h5File.Dataset("chunked").Layout.Class);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_Chunked_implicit_2d()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(
                data,
                dimensions: new[] { 10UL, 10UL },
                chunkDimensions: new[] { 3U, 4U })
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_chunked_implicit_2d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);

            using var h5File = H5File.OpenRead(filePath);
            var nativeDataset = (NativeDataset)h5File.Dataset("chunked");

            Assert.Null(nativeDataset.InternalFilterPipeline);

            var layout = (DataLayoutMessage4)nativeDataset.InternalDataLayout;
            var properties = (ChunkedStoragePropertyDescription4)layout.Properties;

            Assert.Equal(H5DataLayoutClass.Chunked, nativeDataset.Layout.Class);
            Assert.Equal(typeof(ImplicitIndexingInformation), properties.IndexingInformation.GetType());
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_Chunked_fixed_array_filtered_2d()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(
                data,
                dimensions: new[] { 10UL, 10UL },
                chunkDimensions: new[] { 3U, 4U })
        };

        var filePath = Path.GetTempFileName();
       
        var options = new H5WriteOptions(
            Filters: new() {
                ShuffleFilter.Id,
                DeflateFilter.Id
            }
        );

        // Act
        file.Write(filePath, options);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_chunked_fixed_array_filtered_2d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);

            using var h5File = H5File.OpenRead(filePath);
            var nativeDataset = (NativeDataset)h5File.Dataset("chunked");

            Assert.NotNull(nativeDataset.InternalFilterPipeline);

            var layout = (DataLayoutMessage4)nativeDataset.InternalDataLayout;
            var properties = (ChunkedStoragePropertyDescription4)layout.Properties;

            Assert.Equal(H5DataLayoutClass.Chunked, nativeDataset.Layout.Class);
            Assert.Equal(typeof(FixedArrayIndexingInformation), properties.IndexingInformation.GetType());
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
        void action() => file.Write(filePath);

        // Assert
        try
        {
            Assert.Throws<TargetInvocationException>(action);
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
        void action() => file.Write(filePath);

        // Assert
        try
        {
            Assert.Throws<TargetInvocationException>(action);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}