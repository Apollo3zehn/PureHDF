using PureHDF.Experimental;
using Xunit;

namespace PureHDF.Tests.Writing;

public class WritingTests
{
    [Fact]
    public void CanWrite()
    {
        // Arrange
        var dataset_g0_0 = new H5Dataset<ushort>()
        {
            Attributes = new Dictionary<string, object>
            {
                ["implicit_attribute"] = new int[] { -2, -3, -4 }
            }
        };

        var dataset_0 = new H5Dataset<long>();

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

        var file = new Experimental.H5File
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
        file.Save(filePath);

        // Assert
        var expected = File
            .ReadAllText("DumpFiles/writer.dump")
            .Replace("<file-path>", filePath);

        var actual = TestUtils.DumpH5File(filePath);

        Assert.Equal(expected, actual);
    }
}