using Xunit;

namespace PureHDF.Tests.Writing;

public partial class AttributeTests
{
    [Fact]
    public void CanWrite_Mass()
    {
        // Arrange
        var file = new H5File();

        for (int i = 0; i < 1000; i++)
        {
            file.Attributes[i.ToString()] = i;
        }

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/data_mass.dump")
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