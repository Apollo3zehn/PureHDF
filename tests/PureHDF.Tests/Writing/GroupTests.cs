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
        var group_0 = new H5Group
        {
            ["dset"] = new H5Dataset(data: new float[] { 1.1f, 2.2f })
            {
                Attributes = new()
                {
                    ["attr"] = new int[] { -2, -3, -4 }
                }
            },

            Attributes = new()
            {
                ["attr"] = new double[] { 2.0, 3.1, 4.2 }
            }
        };

        var file = new H5File
        {
            ["group_0"] = group_0,

            ["group_1"] = new H5Group
            {
                ["group_0"] = group_0
            },

            ["dataset_0"] = new float[] { 1.1f },

            Attributes = new()
            {
                ["fixed_point_unsigned"] = new uint[] { 1, 2, 3 },
                ["fixed_point_signed"] = new int[] { 1, -2, 3 },
                ["floating_point_32"] = new float[] { 1.1f, -2.2e36f, 3.3f },
                ["floating_point_64"] = new double[] { 1.1, -2.2e36, 3.3 }
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