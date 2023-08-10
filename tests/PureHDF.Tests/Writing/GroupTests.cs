using Xunit;

namespace PureHDF.Tests.Writing;

public class GroupTests
{
    [Fact]
    public void CanWrite_Mass()
    {
        // Arrange
        var file = new H5File();

        for (int i = 0; i < 1000; i++)
        {
            file[i.ToString()] = new H5Group();
        }

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        var actual = TestUtils.DumpH5File(filePath);

        var expected = File
            .ReadAllText("DumpFiles/group_mass.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanWrite_Complex()
    {
        // Arrange
        var dataset_g0_0 = new H5Dataset(data: new float[] { 1.1f, 2.2f })
        {
            Attributes = new Dictionary<string, object>
            {
                ["implicit_attribute"] = new int[] { -2, -3, -4 }
            }
        };

        var dataset_0 = new H5Dataset(data: new float[] { 1.1f });

        var group_0 = new H5Group
        {
            ["dataset_g0_0"] = dataset_g0_0,

            Attributes = new Dictionary<string, object>
            {
                ["attribute_g0_0"] = new double[] { 2.0, 3.1, 4.2 }
            }
        };

        var group_1 = new H5Group
        {
            ["group_0"] = group_0
        };

        var file = new H5File
        {
            ["group_0"] = group_0,
            ["group_1"] = group_1,
            ["dataset_0"] = dataset_0,

            Attributes = new Dictionary<string, object>
            {
                ["attribute_1_fixed_point_unsigned"] = new uint[] { 1, 2, 3 },
                ["attribute_1_fixed_point_signed"] = new int[] { 1, -2, 3 },
                ["attribute_1_floating_point_32"] = new float[] { 1.1f, -2.2e36f, 3.3f },
                ["attribute_1_floating_point_64"] = new double[] { 1.1, -2.2e36, 3.3 }
            }
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Write(filePath);

        // Assert
        var actual = TestUtils.DumpH5File(filePath);
        
        var expected = File
            .ReadAllText("DumpFiles/group_complex.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);
    }
}