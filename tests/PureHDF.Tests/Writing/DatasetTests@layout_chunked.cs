using Xunit;
using System.Reflection;
using PureHDF.Filters;

namespace PureHDF.Tests.Writing;

public partial class DatasetTests
{
    private void CheckIndexType<T>(string filePath, bool filtered) where T : IndexingInformation
    {
        using var h5File = H5File.OpenRead(filePath);
        var nativeDataset = (NativeDataset)h5File.Dataset("chunked");

        if (filtered)
            Assert.NotNull(nativeDataset.InternalFilterPipeline);
        
        else
            Assert.Null(nativeDataset.InternalFilterPipeline);

        var layout = (DataLayoutMessage4)nativeDataset.InternalDataLayout;
        var properties = (ChunkedStoragePropertyDescription4)layout.Properties;

        Assert.Equal(H5DataLayoutClass.Chunked, nativeDataset.Layout.Class);
        Assert.Equal(typeof(T), properties.IndexingInformation.GetType());
    }

    [Fact]
    public void CanWrite_Chunked_single_chunk()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(
                data,
                dimensions: new[] { (ulong)data.Length },
                chunks: new[] { (uint)data.Length })
        };

        var filePath = Path.GetTempFileName();
       
        var options = new H5WriteOptions(
            Filters: new() {
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
                .ReadAllText("DumpFiles/layout_chunked_1d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
            CheckIndexType<SingleChunkIndexingInformation>(filePath, filtered: true);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_Chunked_implicit()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(data, chunks: new[] { 10U })
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_chunked_1d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
            CheckIndexType<ImplicitIndexingInformation>(filePath, filtered: false);
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
                chunks: new[] { 3U, 4U })
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/layout_chunked_2d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
            CheckIndexType<ImplicitIndexingInformation>(filePath, filtered: false);
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
                chunks: new[] { 3U, 4U })
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
                .ReadAllText("DumpFiles/layout_chunked_2d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
            CheckIndexType<FixedArrayIndexingInformation>(filePath, filtered: true);
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
            ["chunked"] = new H5Dataset(data, chunks: new[] { 10U, 10U })
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
            ["chunked"] = new H5Dataset(data, chunks: new[] { 101U })
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