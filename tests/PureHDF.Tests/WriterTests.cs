using System.Drawing;
using PureHDF.Experimental;
using Xunit;

namespace PureHDF.Tests.Writing;

public class ChunkCacheTests
{
    [Fact]
    public void CanWrite()
    {
        // Arrange
        // var attribute_0 = new H5Attribute<Point>(new Point[] { new Point(x: 10, y: 20), new Point(x: 20, y: 30) });
        var attribute_1 = new H5Attribute<uint>(new uint[] { 1, 2, 3 });
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
                // [nameof(attribute_0)] = attribute_0,
                [nameof(attribute_1)] = attribute_1
            }
        };

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);
        using var actual = H5File.InternalOpenRead(filePath, deleteOnClose: true);

        // Assert

        // TODO compare dump instead?
        var actual_attribute_1 = actual.Attribute("attribute_1");
        var actual_attribute_1_data = actual_attribute_1.Read<uint>();

        var actual_group_0 = actual.Group("group_0");
        var actual_group_0_attribute_1 = actual_group_0.Attribute("attribute_g0_0");
        var actual_group_0_attribute_1_data = actual_group_0_attribute_1.Read<byte>();

        Assert.True(attribute_g0_0.Data.ToArray().SequenceEqual(actual_group_0_attribute_1_data.ToArray()));

        var actual_group_1 = actual.Group("group_1");
    }
}