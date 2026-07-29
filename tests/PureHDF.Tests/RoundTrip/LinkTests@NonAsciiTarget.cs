using Xunit;

namespace PureHDF.Tests.RoundTrip;

public class SoftLinkRoundTripTests
{
    [Theory]
    [InlineData("PD")]              // ASCII, worked before
    [InlineData("µA")]              // 2-byte
    [InlineData("温度")]             // 3-byte
    [InlineData("data\U0001F600")]  // 4-byte
    public void CanFollowSoftLinkWithNonAsciiTarget(string target)
    {
        // Arrange
        var h5FileWrite = new H5File
        {
            [target] = new H5Group { ["data"] = new H5Dataset(new int[] { 1, 2, 3 }) },
            ["link"] = new H5SoftLink($"/{target}")
        };

        var memoryStream = new MemoryStream();
        h5FileWrite.Write(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var h5FileRead = H5File.Open(memoryStream);

        // Act
        var resolved = h5FileRead.Get("link");

        // Assert
        // The target group's own name decodes correctly because LinkMessage forwards its
        // character set. The soft link's value is a path built from such names, so decoding
        // it as ASCII leaves it naming an object that does not exist and the link resolves
        // to an unresolved-link stub instead of the group.
        Assert.True(h5FileRead.LinkExists(target), "the target group itself is missing");
        Assert.Null((resolved as IH5UnresolvedLink)?.Reason);
        Assert.IsAssignableFrom<IH5Group>(resolved);
        Assert.NotNull(((IH5Group)resolved).Dataset("data"));
    }
}
