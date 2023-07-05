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
        var attribute_0 = new H5Attribute<Point>("attribute_0", new Point[] { new Point(x: 10, y: 20), new Point(x: 20, y: 30) });
        var attribute_1 = new H5Attribute<uint>("attribute_1", new uint[] { 1, 2, 3 });
        var attribute_g0_0 = new H5Attribute<double>("attribute_g0_0", new double[] { 2.0, 3.1, 4.2 });
        var attribute_g0_d0_0 = new H5Attribute<int>("attribute_g0_d0_0", new int[] { -2, -3, -4 });

        var dataset_g0_0 = new H5Dataset<ushort>(
            Name: "dataset_g0_0",
            Attributes: new List<H5AttributeBase>() { attribute_g0_d0_0 }
        );

        var dataset_0 = new H5Dataset<long>(
            Name: "dataset_0",
            Attributes: new List<H5AttributeBase>()
        );

        var group_0 = new H5Group(
            Name: "group0", 
            Attributes: new List<H5AttributeBase>() { attribute_g0_0 },
            Objects: new List<H5Object>() { dataset_g0_0 });

        var group_1 = new H5Group(
            Name: "group1",
            Objects: new List<H5Object>() { group_0 /* test same group hard-linked twice */ });

        var expected = new Experimental.H5File(
            Attributes: new List<H5AttributeBase>() { attribute_0, attribute_1 },
            Objects: new List<H5Object>() { group_0 /* test same group hard-linked twice */, group_1, dataset_0 });

        var filePath = Path.GetTempFileName();

        // Act
        expected.Save(filePath);
        using var actual = H5File.InternalOpenRead(filePath, deleteOnClose: true);

        // Assert
        var _1 = actual.Group("group0");
        var _2 = actual.Group("group1");
    }
}