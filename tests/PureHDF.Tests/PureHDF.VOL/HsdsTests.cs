using PureHDF.VOL.Hsds;
using Xunit;

namespace PureHDF.Tests.Reading.VOL;

public class HsdsTests : IClassFixture<HsdsTestsFixture>
{
    private readonly IHsdsConnector _connector;

    public HsdsTests(HsdsTestsFixture fixture)
    {
        _connector = fixture.Connector;
    }

    [Theory]
    [InlineData("/g1/g1.1/dset1.1.1", true)]
    [InlineData("g1/g1.1/dset1.1.1", true)]
    [InlineData("/g1/I do not exist", false)]
    public void CanCheckLinkExists(string path, bool expected)
    {
        // Arrange

        // Act
        var actual = _connector
            .LinkExists(path);

        // Assert
        Assert.Equal(expected, actual);
    }

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

    [Fact(Skip = "wait for https://github.com/HDFGroup/hdf-rest-api/issues/13")]
    public void CanCheckAttributeExists()
    {
        //
    }
    // [Theory]
    // [InlineData("attr1", true)]
    // [InlineData("attr3", false)]
    // public void CanCheckAttributeExists(string path, bool expected)
    // {
    //     // Arrange

    //     // Act
    //     var actual = _connector
    //         .AttributeExists(path);

    //     // Assert
    //     Assert.Equal(expected, actual);
    // }

    [Fact(Skip = "wait for https://github.com/HDFGroup/hdf-rest-api/issues/13")]
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
}