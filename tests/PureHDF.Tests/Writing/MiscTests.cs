using Xunit;

namespace PureHDF.Tests.Writing;

public class MiscTests
{
    [Fact]
    public void CanWrite_WithUserBlock()
    {
        // Arrange
        var file = new H5File
        {
            ["g"] = new H5Group
            {
                ["d"] = new H5Dataset(1.1, [1])
            },
            Attributes =
            {
                ["a"] = 1
            }
        };

        string filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath, new H5WriteOptions { UserBlockSize = 512 });

        // Assert
        try
        {
            string? actual = TestUtils.DumpH5File(filePath);

            string expected = File
                .ReadAllText("DumpFiles/misc_with_user_block.dump")
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
    public void CanWrite_MoreThanOneGlobalHeapCollection()
    {
        // Arrange
        var file = new H5File();

        for (int i = 0; i < 100; i++) file.Attributes[i.ToString()] = $"The attribute content {i}.";

        string filePath = Path.GetTempFileName();

        // Act - the global heap is where a VARIABLE-length string goes, and an attribute is measured into a
        // fixed-length one by default, so the collections this is about only exist if that is turned off.
        file.Write(filePath, new H5WriteOptions
        {
            AttributeStringLength = H5AttributeStringLength.VariableLength
        });

        // Assert
        try
        {
            string? actual = TestUtils.DumpH5File(filePath);

            string expected = File
                .ReadAllText("DumpFiles/misc_global_heap_collections.dump")
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
    public void CanWrite_ObjectLargerThanMinimum()
    {
        // Arrange
        var file = new H5File();
        string lorem =
            "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua.";

        file.Attributes["large"] = string
            .Join(' ', Enumerable.Range(0, 27).Select(_ => lorem));

        string filePath = Path.GetTempFileName();

        // Act - variable-length, so that the value lands in a global heap collection rather than in a
        // measured fixed-length attribute.
        file.Write(filePath, new H5WriteOptions
        {
            AttributeStringLength = H5AttributeStringLength.VariableLength
        });

        // Assert
        try
        {
            string? actual = TestUtils.DumpH5File(filePath);

            string expected = File
                .ReadAllText("DumpFiles/misc_global_heap_collection_large.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}