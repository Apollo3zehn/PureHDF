using PureHDF.Experimental;
using Xunit;

namespace PureHDF.Tests.Writing;

public class WriterTests
{
    [Fact]
    public void CanWrite()
    {
        // Arrange
        // var attribute_0 = new H5Attribute<Point>(new Point[] { new Point(x: 10, y: 20), new Point(x: 20, y: 30) });
        var attribute_1_fixed_point_unsigned = new H5Attribute<uint>(new uint[] { 1, 2, 3 });
        var attribute_1_fixed_point_signed = new H5Attribute<int>(new int[] { 1, -2, 3 });
        var attribute_1_floating_point_32 = new H5Attribute<float>(new float[] { 1.1f, -2.2e36f, 3.3f });
        var attribute_1_floating_point_64 = new H5Attribute<double>(new double[] { 1.1, -2.2e36, 3.3 });
        var attribute_g0_0 = new H5Attribute<double>(new double[] { 2.0, 3.1, 4.2 });

        var dataset_g0_0 = new H5Dataset<ushort>()
        {
            Attributes = new Dictionary<string, H5AttributeBase>
            {
                ["implicit_attribute"] = new int[] { -2, -3, -4 }
            }
        };

        var dataset_0 = new H5Dataset<long>();

        var group_0 = new H5Group
        {
            ["dataset_g0_0"] = dataset_g0_0,

            Attributes = new Dictionary<string, H5AttributeBase>
            {
                ["attribute_g0_0"] = attribute_g0_0
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

            Attributes = new Dictionary<string, H5AttributeBase>
            {
                [nameof(attribute_1_fixed_point_unsigned)] = attribute_1_fixed_point_unsigned,
                [nameof(attribute_1_fixed_point_signed)] = attribute_1_fixed_point_signed,
                [nameof(attribute_1_floating_point_32)] = attribute_1_floating_point_32,
                [nameof(attribute_1_floating_point_64)] = attribute_1_floating_point_64
            }
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var expected = File
            .ReadAllText("TestFiles/expected.writertests.dump")
            .Replace("<file-path>", filePath);

        var actual = TestUtils.DumpH5File(filePath);

        Assert.Equal(expected, actual);
    }
}