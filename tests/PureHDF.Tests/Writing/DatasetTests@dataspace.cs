using Xunit;

namespace PureHDF.Tests.Writing;

public partial class DatasetTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanWrite_Space_Null(bool preferCompact)
    {
        // Arrange
        var file = new H5File();

        file["Null"] = new H5Dataset<int[]>();

        var filePath = Path.GetTempFileName();
        var options = new H5WriteOptions(PreferCompactDatasetLayout: preferCompact);

        // Act
        file.Write(filePath, options);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/space_null.dump")
                .Replace("<file-path>", filePath)
                .Replace("<type>", "DATASET");

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// A null dataspace holds no elements, so the dataset carries no data - whatever its datatype.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanWrite_Space_Null_ScalarDatatype(bool preferCompact)
    {
        // Arrange
        var file = new H5File();

        file["NullInt"] = new H5Dataset<int>();
        file["NullString"] = new H5Dataset<string>();

        var filePath = Path.GetTempFileName();
        var options = new H5WriteOptions(PreferCompactDatasetLayout: preferCompact);

        // Act
        file.Write(filePath, options);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/space_null_scalar.dump")
                .Replace("<file-path>", filePath)
                .Replace("<type>", "DATASET");

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// A scalar whose value is null is one element that is null, not an absence of elements.
    /// </summary>
    [Fact]
    public void CanWrite_Space_Scalar_NullValue()
    {
        // Arrange - the value cast rather than 'default', which binds to the null-dataspace constructor.
        var file = new H5File();

        file["text"] = new H5Dataset<string?>((string?)null);

        var stream = new MemoryStream();

        // Act
        file.Write(stream);

        // Assert
        stream.Seek(0, SeekOrigin.Begin);

        using var actual = H5File.Open(stream);

        Assert.Equal(H5DataspaceType.Scalar, actual.Dataset("text").Space.Type);
        Assert.Null(actual.Dataset("text").Read<string?>());
    }

    [Fact]
    public void CanWrite_Space_2D()
    {
        // Arrange
        var file = new H5File();
        var data = new int[9] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var fileDims = new ulong[] { 3, 3 };

        file["2D"] = new H5Dataset(data, fileDims: fileDims);

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/space_2D.dump")
                .Replace("<file-path>", filePath)
                .Replace("<type>", "DATASET");

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
