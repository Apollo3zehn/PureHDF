using Xunit;

namespace PureHDF.Tests.Writing;

public partial class AttributeTests
{
    [Fact]
    public void CanWrite_Space_Null()
    {
        // Arrange
        var file = new H5File();

        file.Attributes["Null"] = new H5Attribute<int[]>();

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/space_null.dump")
                .Replace("<file-path>", filePath)
                .Replace("<type>", "ATTRIBUTE");

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// A null dataspace holds no elements, so the attribute carries no data - whatever its datatype.
    /// </summary>
    /// <remarks>
    /// CanWrite_Space_Null covers int[], whose encoder walks a span and writes nothing when handed an
    /// empty one. A SCALAR datatype is encoded by indexing element zero instead, which a null dataspace
    /// does not have, so it threw IndexOutOfRangeException - including for the plain
    /// <c>new H5Attribute&lt;int&gt;()</c> form.
    /// </remarks>
    [Fact]
    public void CanWrite_Space_Null_ScalarDatatype()
    {
        // Arrange
        var file = new H5File();

        file.Attributes["NullInt"] = new H5Attribute<int>();
        file.Attributes["NullString"] = new H5Attribute<string>();

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/space_null_scalar.dump")
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
    public void CanWrite_Space_2D()
    {
        // Arrange
        var file = new H5File();
        var data = new int[9] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var dimensions = new ulong[] { 3, 3 };

        file.Attributes["2D"] = new H5Attribute(data, dimensions);

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
                .Replace("<type>", "ATTRIBUTE");

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}