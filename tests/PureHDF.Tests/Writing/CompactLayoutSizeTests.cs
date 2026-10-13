using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     Which datasets are written compact: those whose data, variable-length values included, fits
///     <see cref="H5WriteOptions.MaximumCompactDatasetSize" />.
/// </summary>
/// <remarks>
///     A compact dataset's data is part of its object header, so it is structure, and a front-loaded file
///     keeps it at the front with the rest of the structure. Small data is cheaper there - a reader that
///     opens the dataset already has it. Large data turns the structure into payload, which is what
///     front loading exists to keep apart. A variable-length dataset's values count, since the header
///     names them and they are placed with it.
/// </remarks>
public class CompactLayoutSizeTests
{
    [Theory]
    [InlineData(512, H5DataLayoutClass.Contiguous)]
    [InlineData(1024, H5DataLayoutClass.Compact)]
    public void FixedSizeDataIsCompactUpToTheMaximum(int maximum, H5DataLayoutClass expected)
    {
        // 800 bytes
        var data = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();

        var actual = WriteAndReadLayout(data, new H5WriteOptions { MaximumCompactDatasetSize = maximum });

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(16, H5DataLayoutClass.Compact)]
    [InlineData(8 * 1024, H5DataLayoutClass.Contiguous)]
    public void VariableLengthValuesCountTowardsTheMaximum(int valueLength, H5DataLayoutClass expected)
    {
        // 8 heap IDs are 128 bytes either way; the values are 8 x 16 bytes or 8 x 8 kB
        var data = Enumerable.Range(0, 8).Select(i => new string((char)('a' + i), valueLength)).ToArray();

        var actual = WriteAndReadLayout(data, new H5WriteOptions { MaximumCompactDatasetSize = 4096 });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValuesMeasuredForTheLayoutGetOneCollectionSizedToThem()
    {
        // Arrange - measured to decide the layout, too large to be compact, then written contiguous
        var data = Enumerable.Range(0, 8).Select(i => new string((char)('a' + i), 8 * 1024)).ToArray();
        var file = new H5File { ["d"] = new H5Dataset(data) };
        var stream = new MemoryStream();

        // Act
        file.Write(stream, new H5WriteOptions { MaximumCompactDatasetSize = 4096 });

        // Assert - one collection: the header, an object per value, and object 0 for the free space
        var bytes = stream.ToArray();
        var signature = "GCOL"u8;
        var offset = bytes.AsSpan().IndexOf(signature);

        Assert.True(offset >= 0);
        Assert.Equal(-1, bytes.AsSpan(offset + 4).IndexOf(signature));
        Assert.Equal(16UL + 8 * (16 + 8 * 1024) + 16, BitConverter.ToUInt64(bytes, offset + 8));

        stream.Position = 0;
        using var actual = H5File.Open(stream);
        Assert.Equal(data, actual.Dataset("d").Read<string[]>());
    }

    [Fact]
    public void ANegativeMaximumIsRejected()
    {
        var file = new H5File { ["d"] = new H5Dataset(new double[1]) };

        Assert.ThrowsAny<Exception>(() => file.Write(new MemoryStream(), new H5WriteOptions { MaximumCompactDatasetSize = -1 }));
    }

    private static H5DataLayoutClass WriteAndReadLayout<T>(T[] data, H5WriteOptions options)
    {
        var file = new H5File { ["d"] = new H5Dataset(data) };
        var filePath = Path.GetTempFileName();

        try
        {
            file.Write(filePath, options);
            TestUtils.DumpH5File(filePath);

            using var actual = H5File.OpenRead(filePath);
            var dataset = actual.Dataset("d");

            Assert.Equal(data, dataset.Read<T[]>());

            return dataset.Layout.Class;
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
