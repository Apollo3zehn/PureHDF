using Xunit;

namespace PureHDF.Tests.Writing;

public class MiscTests
{
    [Fact]
    public void CanWrite_MoreThanOneGlobalHeapCollection()
    {
        // Arrange
        var file = new H5File();

        for (int i = 0; i < 100; i++)
        {
            file.Attributes[i.ToString()] = $"The attribute content {i}.";
        }

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/misc_global_heap_collections.dump")
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