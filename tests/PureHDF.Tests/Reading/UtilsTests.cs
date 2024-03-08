using Xunit;

namespace PureHDF.Tests.Reading;

public class UtilsTests
{
    [Fact]
    public void CanConvertCoordinatesToLinearIndex()
    {
        // Arrange
        var coordinates = new ulong[] { 10, 20, 30 };
        var dimensions = new ulong[] { 11, 22, 33 };
        var expected = 7950UL;

        // Act
        var actual = MathUtils.ToLinearIndex(coordinates, dimensions);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanConvertLinearIndexToCoordinates()
    {
        // Arrange
        var dimensions = new ulong[] { 11, 22, 33 };
        var linearIndex = 7950UL;
        var expected = new ulong[] { 10, 20, 30 };

        // Act
        var actual = MathUtils.ToCoordinates(linearIndex, dimensions);
        
        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }
}