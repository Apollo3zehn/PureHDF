using PureHDF.VOL.Hsds;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading.VOL;

public class HsdsTests(HsdsTestsFixture fixture) : IClassFixture<HsdsTestsFixture>
{
    private readonly HsdsTestsFixture _fixture = fixture;

    private IHsdsConnector RequireConnector()
    {
        Skip.IfNot(_fixture.Connector is not null, _fixture.SkipReason);
        return _fixture.Connector!;
    }

    [SkippableFact]
    public void CanGetGroup()
    {
        // Arrange
        var connector = RequireConnector();
        var expected = "g1.1";

        // Act
        var actual = connector
            .Group($"/g1/{expected}")
            .Name;

        // Assert
        Assert.Equal(expected, actual);
    }

    [SkippableFact]
    public void CanGetChildren()
    {
        // Arrange
        var connector = RequireConnector();

        // Act
        var actual = connector
            .Children();

        // Assert
        Assert.Collection(actual,
            child => Assert.Equal("g1", child.Name),
            child => Assert.Equal("g2", child.Name));
    }

    [SkippableFact]
    public void CanGetAttribute()
    {
        // Arrange
        var connector = RequireConnector();
        var expected = "attr1";

        // Act
        var actual = connector
            .Attribute(expected);

        // Assert
        Assert.Equal(expected, actual.Name);
    }

    [SkippableFact]
    public void CanGetAttributes()
    {
        // Arrange
        var connector = RequireConnector();

        // Act
        var actual = connector
            .Attributes();

        // Assert
        Assert.Collection(actual,
            attribute => Assert.Equal("attr1", attribute.Name),
            attribute => Assert.Equal("attr2", attribute.Name));
    }

    [SkippableFact]
    public void CanReadAttribute()
    {
        // Arrange
        var connector = RequireConnector();
        var expected = new int[] { 97, 98, 99, 100, 101, 102, 103, 104, 105, 0 };

        // Act
        var actual = connector
            .Attribute("attr1")
            .Read<int[]>();

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }

    [SkippableFact]
    public void CanGetDataset()
    {
        // Arrange
        var connector = RequireConnector();
        var expected = "dset1.1.1";

        // Act
        var actual = connector
            .Dataset($"/g1/g1.1/{expected}");

        // Assert
        Assert.Equal(expected, actual.Name);
    }

    [SkippableFact]
    public void CanReadDataset()
    {
        // Arrange
        var connector = RequireConnector();
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
        var actual = connector
            .Dataset("/g1/g1.1/dset1.1.1")
            .Read<int[]>();

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }

    [SkippableFact]
    public void CanReadDatasetWithFileSelection_Hyperslab()
    {
        // Arrange
        var connector = RequireConnector();
        var expected = new int[] { 6, 10, 14, 15, 25, 35 };

        var fileSelection = new HyperslabSelection(
            rank: 2,
            starts: [2, 3],
            strides: [3, 2],
            counts: [2, 3],
            blocks: [1, 1]
        );

        // Act
        var actual = connector
            .Dataset("/g1/g1.1/dset1.1.1")
            .Read<int[]>(fileSelection);

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }

    [SkippableFact]
    public void CanReadDatasetWithFileSelection_Point()
    {
        // Arrange
        var connector = RequireConnector();
        var expected = new int[] { 0, 2, 6, 12 };

        var fileSelection = new PointSelection(new ulong[,] {
            { 0, 1 },
            { 1, 2 },
            { 2, 3 },
            { 3, 4 }
        });

        // Act
        var actual = connector
            .Dataset("/g1/g1.1/dset1.1.1")
            .Read<int[]>(fileSelection);

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }
}