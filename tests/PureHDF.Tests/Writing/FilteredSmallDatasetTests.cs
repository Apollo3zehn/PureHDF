using PureHDF.Filters;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     A filter set for every dataset through <see cref="H5WriteOptions.Filters" /> leaves out datasets of up
///     to 64 bytes, which are written compact instead.
/// </summary>
/// <remarks>
///     A filter needs a chunked layout, and compressing a few bytes costs more than it saves: measured on
///     20,000 datasets of one double each, deflate made the file 21% larger and the write 70% slower than
///     writing them compact. By 256 bytes, compressible data is the other way round - 42% smaller
///     deflated - so the bound is below the one for compact layout in general. A filter given for the
///     dataset itself is applied regardless - that is a request about this dataset, not a default for all
///     of them.
/// </remarks>
public class FilteredSmallDatasetTests
{
    [Theory]
    [InlineData(8, H5DataLayoutClass.Compact)]
    [InlineData(9, H5DataLayoutClass.Chunked)]
    [InlineData(32, H5DataLayoutClass.Chunked)]
    public void AFilterForEveryDatasetLeavesOutTheSmallestDatasets(int count, H5DataLayoutClass expected)
    {
        // 64 bytes, 72, and 256 - compact without the filter, but compressing well enough to keep it
        var data = Enumerable.Range(0, count).Select(i => (double)i).ToArray();
        var dataset = new H5Dataset(data);

        var actual = WriteAndReadLayout(dataset, data, new H5WriteOptions(Filters: [DeflateFilter.Id]));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void VariableLengthValuesDecideToo()
    {
        // 8 heap IDs (128 bytes) naming 8 kB of values: not compact, so filtered
        var data = Enumerable.Range(0, 8).Select(i => new string((char)('a' + i), 1024)).ToArray();
        var dataset = new H5Dataset(data);

        var actual = WriteAndReadLayout(dataset, data, new H5WriteOptions(Filters: [DeflateFilter.Id]));

        Assert.Equal(H5DataLayoutClass.Chunked, actual);
    }

    [Fact]
    public void AFilterGivenForTheDatasetIsApplied()
    {
        var data = Enumerable.Range(0, 8).Select(i => (double)i).ToArray();
        var dataset = new H5Dataset(data, datasetCreation: new H5DatasetCreation(Filters: [DeflateFilter.Id]));

        var actual = WriteAndReadLayout(dataset, data, new H5WriteOptions());

        Assert.Equal(H5DataLayoutClass.Chunked, actual);
    }

    [Fact]
    public void ChunksGivenForTheDatasetAreFiltered()
    {
        var data = Enumerable.Range(0, 8).Select(i => (double)i).ToArray();
        var dataset = new H5Dataset(data, chunks: [4]);

        var actual = WriteAndReadLayout(dataset, data, new H5WriteOptions(Filters: [DeflateFilter.Id]));

        Assert.Equal(H5DataLayoutClass.Chunked, actual);
    }

    [Fact]
    public void ADatasetWrittenLaterIsFiltered()
    {
        // Never compact, since its data is not there when its header is written
        var data = Enumerable.Range(0, 8).Select(i => (double)i).ToArray();
        var dataset = new H5Dataset<double[]>(fileDims: [(ulong)data.Length]);
        var file = new H5File { ["d"] = dataset };
        var filePath = Path.GetTempFileName();

        try
        {
            using (var writer = file.BeginWrite(filePath, new H5WriteOptions(Filters: [DeflateFilter.Id])))
                writer.Write(dataset, data);

            using var actual = H5File.OpenRead(filePath);

            Assert.Equal(H5DataLayoutClass.Chunked, actual.Dataset("d").Layout.Class);
            Assert.Equal(data, actual.Dataset("d").Read<double[]>());
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private static H5DataLayoutClass WriteAndReadLayout<T>(H5Dataset dataset, T[] data, H5WriteOptions options)
    {
        var file = new H5File { ["d"] = dataset };
        var filePath = Path.GetTempFileName();

        try
        {
            file.Write(filePath, options);
            TestUtils.DumpH5File(filePath);

            using var actual = H5File.OpenRead(filePath);
            var read = actual.Dataset("d");

            Assert.Equal(data, read.Read<T[]>());

            return read.Layout.Class;
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
