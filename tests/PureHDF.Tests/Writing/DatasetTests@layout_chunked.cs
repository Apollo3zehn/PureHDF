using Xunit;
using System.Reflection;
using HDF.PInvoke;
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
    public void CanWrite_Chunked_single_chunk_filtered()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(
                data,
                chunks: [(uint)data.Length])
        };

        var filePath = Path.GetTempFileName();

        var options = new H5WriteOptions(
            Filters:
            [
                DeflateFilter.Id
            ]
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
    public void CanWrite_Chunked_single_chunk_filtered_Deferred()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset<int[]>(
            fileDims: [(ulong)data.Length],
            chunks: [(uint)data.Length]
        );

        var file = new H5File
        {
            ["chunked"] = dataset
        };

        var filePath = Path.GetTempFileName();

        var options = new H5WriteOptions(
            Filters:
            [
                DeflateFilter.Id
            ]
        );

        // Act
        using (var writer = file.BeginWrite(filePath, options))
        {
            writer.Write(dataset, data);
        }

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
            ["chunked"] = new H5Dataset(data, chunks: [10U])
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
    public void CanWrite_Chunked_implicit_Deferred()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset<int[]>(
            fileDims: [(ulong)data.Length],
            chunks: [10U]
        );

        var file = new H5File
        {
            ["chunked"] = dataset
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
                fileDims: [10UL, 10UL],
                chunks: [3U, 4U])
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
                fileDims: [10UL, 10UL],
                chunks: [3U, 4U])
        };

        var filePath = Path.GetTempFileName();

        var options = new H5WriteOptions(
            Filters:
            [
                ShuffleFilter.Id,
                DeflateFilter.Id
            ]
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
    public void CanWrite_Chunked_fixed_array_filtered_2d_Deferred()
    {
        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset<int[]>(
            fileDims: [10UL, 10UL],
            chunks: [3U, 4U]
        );

        var file = new H5File
        {
            ["chunked"] = dataset
        };

        var filePath = Path.GetTempFileName();

        var options = new H5WriteOptions(
            Filters: 
            [
                ShuffleFilter.Id,
                DeflateFilter.Id
            ]
        );

        // Act
        using (var writer = file.BeginWrite(filePath, options))
        {
            writer.Write(dataset, data);
        }

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
            ["chunked"] = new H5Dataset(data, chunks: [10U, 10U])
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
            ["chunked"] = new H5Dataset(data, chunks: [101U])
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

    // Cross-library compatibility test for chunk dimension encoded length.
    // Pre-fix: chunked layouts always wrote (byte)8 as the encoded length, which
    // libhdf5's H5D__chunk_set_sizes() rejects with
    //   "stored chunk dimension encoding length does not match value calculated from chunk dimensions"
    // because libhdf5 expects the *minimum* number of bytes needed to hold the
    // largest chunk dimension. This test writes a chunked file through PureHDF
    // and opens it through libhdf5 (via HDF.PInvoke); regression of the
    // encoded-length bug surfaces as H5F.open returning a negative handle.
    [Theory]
    [InlineData(new uint[] { 10U })]                            // 1D, max 10 → 1 byte
    [InlineData(new uint[] { 256U })]                           // 1D, max 256 → 2 bytes
    [InlineData(new uint[] { 65536U })]                         // 1D, max 65536 → 3 bytes
    [InlineData(new uint[] { 4U, 4U, 32U, 32U, 16U, 1U })]      // 6D real-world (microscopy)
    public void ChunkedFile_IsReadableBy_libhdf5(uint[] chunkDims)
    {
        // Arrange — build N-D mock data matching the chunk shape (one chunk per dim)
        var totalElements = 1;
        foreach (var d in chunkDims)
            totalElements *= (int)d;
        var rawData = new int[totalElements];
        for (var i = 0; i < totalElements; i++)
            rawData[i] = i;

        Array data;
        if (chunkDims.Length == 1)
        {
            data = rawData;
        }
        else
        {
            var shape = new int[chunkDims.Length];
            for (var i = 0; i < chunkDims.Length; i++)
                shape[i] = (int)chunkDims[i];
            var nd = Array.CreateInstance(typeof(int), shape);
            Buffer.BlockCopy(rawData, 0, nd, 0, rawData.Length * sizeof(int));
            data = nd;
        }

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(data, chunks: chunkDims)
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert — libhdf5 must accept the file (negative handle = error)
        try
        {
            var fileId = H5F.open(filePath, H5F.ACC_RDONLY);
            try
            {
                Assert.True(fileId >= 0, $"H5F.open rejected PureHDF chunked file (handle={fileId})");

                var datasetId = H5D.open(fileId, "chunked");
                try
                {
                    Assert.True(datasetId >= 0, $"H5D.open rejected chunked dataset (handle={datasetId})");
                }
                finally
                {
                    if (datasetId >= 0)
                        _ = H5D.close(datasetId);
                }
            }
            finally
            {
                if (fileId >= 0)
                    _ = H5F.close(fileId);
            }
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}