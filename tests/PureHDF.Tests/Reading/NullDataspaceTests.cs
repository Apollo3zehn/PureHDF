using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// An object with a null dataspace holds no elements, and reading one yields no elements.
/// </summary>
/// <remarks>
/// There is nothing to decode, but "nothing" is an answer an array-typed read can represent.
/// <para>
/// A SCALAR result type is the case that cannot be answered, since there is no element to return and
/// no way for the result type to say so. That still throws, but says why.
/// </para>
/// </remarks>
public class NullDataspaceTests
{
    private static H5File CreateFile()
    {
        var file = new H5File();

        file.Attributes["null_space_int32"] = new H5Attribute<int[]>();
        file["null_space_int32"] = new H5Dataset<int[]>();

        return file;
    }

    private static NativeFile RoundTrip(H5File file)
    {
        var stream = new MemoryStream();
        file.Write(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return H5File.Open(stream);
    }

    [Fact]
    public void AScalarReadOfANullDataspaceAttributeSaysWhyItCannot()
    {
        using var actual = RoundTrip(CreateFile());

        var exception = Assert.Throws<InvalidOperationException>(
            () => actual.Attribute("null_space_int32").Read<int>());

        Assert.Contains("null dataspace", exception.Message);
        Assert.Contains("null_space_int32", exception.Message);
    }

    [Fact]
    public void AScalarReadOfANullDataspaceDatasetSaysWhyItCannot()
    {
        using var actual = RoundTrip(CreateFile());

        var exception = Assert.Throws<InvalidOperationException>(
            () => actual.Dataset("null_space_int32").Read<int>());

        Assert.Contains("null dataspace", exception.Message);
        Assert.Contains("null_space_int32", exception.Message);
    }

    /// <summary>The element type still has to match, so a null dataspace is not a way past decoding.</summary>
    [Fact]
    public void AnIncompatibleElementTypeStillThrows()
    {
        using var actual = RoundTrip(CreateFile());

        Assert.ThrowsAny<Exception>(() => actual.Attribute("null_space_int32").Read<string[]>());
    }
}
