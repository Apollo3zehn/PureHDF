using Xunit;

namespace PureHDF.Tests.Writing;

public partial class AttributeTests
{
    [Fact]
    public void CanWrite_OnGroup()
    {
        // Arrange
        var group = new H5Group();

        var file = new H5File
        {
            ["group"] = group
        };

        group.Attributes["attribute 1"] = 99.2;
        group.Attributes["attribute 2"] = 99.3;

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/attribute_on_group.dump")
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

    [Fact]
    public void CanWrite_OnDataset()
    {
        // Arrange
        var dataset = new H5Dataset(99.1);

        var file = new H5File
        {
            ["dataset"] = dataset
        };

        dataset.Attributes["attribute 1"] = 99.2;
        dataset.Attributes["attribute 2"] = 99.3;

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText($"DumpFiles/attribute_on_dataset.dump")
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