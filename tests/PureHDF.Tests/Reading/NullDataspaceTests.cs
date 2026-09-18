using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// An object with a null dataspace holds no elements, and reading one yields no elements.
/// </summary>
/// <remarks>
/// Refusing the read outright made a shape that other tools produce routinely unreadable: h5py's
/// <c>Empty</c> writes exactly this, so any file carrying an empty attribute had one PureHDF could
/// enumerate and then not read. There is nothing to decode, but "nothing" is an answer an array-typed
/// read can represent.
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

        file.Attributes["empty_i4"] = new H5Attribute<int[]>();

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
            () => actual.Attribute("empty_i4").Read<int>());

        Assert.Contains("null dataspace", exception.Message);
        Assert.Contains("empty_i4", exception.Message);
    }

    /// <summary>The element type still has to match, so a null dataspace is not a way past decoding.</summary>
    [Fact]
    public void AnIncompatibleElementTypeStillThrows()
    {
        using var actual = RoundTrip(CreateFile());

        Assert.ThrowsAny<Exception>(() => actual.Attribute("empty_i4").Read<string[]>());
    }
}
