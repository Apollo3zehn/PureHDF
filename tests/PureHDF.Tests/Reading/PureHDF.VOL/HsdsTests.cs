using PureHDF.VOL.Hsds;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading.VOL;

public class HsdsTests(HsdsTestsFixture fixture) : IClassFixture<HsdsTestsFixture>
{
    private readonly IHsdsConnector _connector = fixture.Connector;

    [Fact]
    public void CanGetGroup()
    {
        // Arrange
        var expected = "g1.1";

        // Act
        var actual = _connector
            .Group($"/g1/{expected}")
            .Name;

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanGetChildren()
    {
        // Arrange

        // Act
        var actual = _connector
            .Children();

        // Assert
        Assert.Collection(actual,
            child => Assert.Equal("g1", child.Name),
            child => Assert.Equal("g2", child.Name));
    }

    [Fact]
    public void CanGetAttribute()
    {
        // Arrange
        var expected = "attr1";

        // Act
        var actual = _connector
            .Attribute(expected);

        // Assert
        Assert.Equal(expected, actual.Name);
    }

    [Fact]
    public void CanGetAttributes()
    {
        // Arrange

        // Act
        var actual = _connector
            .Attributes();

        // Assert
        Assert.Collection(actual,
            attribute => Assert.Equal("attr1", attribute.Name),
            attribute => Assert.Equal("attr2", attribute.Name));
    }

    [Fact]
    public void CanReadAttribute()
    {
        // Arrange
        var expected = new int[] { 97, 98, 99, 100, 101, 102, 103, 104, 105, 0 };

        // Act
        var actual = _connector
            .Attribute("attr1")
            .Read<int[]>();

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }

    [Fact]
    public void CanGetDataset()
    {
        // Arrange
        var expected = "dset1.1.1";

        // Act
        var actual = _connector
            .Dataset($"/g1/g1.1/{expected}");

        // Assert
        Assert.Equal(expected, actual.Name);
    }

    [Fact]
    public void CanReadDataset()
    {
        // Arrange
        var expected = new int[100];

        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                expected[i * 10 + j] = i * j;
            }
        }

        // TODO: handle memory selections

        // Act
        var actual = _connector
            .Dataset("/g1/g1.1/dset1.1.1")
            .Read<int>();

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }

    [Fact]
    public void CanReadDatasetWithFileSelection_Hyperslab()
    {
        // Arrange
        var expected = new int[] { 6, 10, 14, 15, 25, 35 };

        var fileSelection = new HyperslabSelection(
            rank: 2,
            starts: new ulong[] { 2, 3 },
            strides: new ulong[] { 3, 2 },
            counts: new ulong[] { 2, 3 },
            blocks: new ulong[] { 1, 1 }
        );

        // Act
        var actual = _connector
            .Dataset("/g1/g1.1/dset1.1.1")
            .Read<int>(fileSelection);

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }

    [Fact]
    public void CanReadDatasetWithFileSelection_Point()
    {
        // Arrange
        var expected = new int[] { 0, 2, 6, 12 };

        var fileSelection = new PointSelection(new ulong[,] {
            { 0, 1 },
            { 1, 2 },
            { 2, 3 },
            { 3, 4 }
        });

        // Act
        var actual = _connector
            .Dataset("/g1/g1.1/dset1.1.1")
            .Read<int>(fileSelection);

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }
}