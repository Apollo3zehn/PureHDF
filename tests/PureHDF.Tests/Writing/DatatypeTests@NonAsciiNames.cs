using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
/// A compound member name has no character set field in the specification, and the reference
/// library copies it as an opaque NUL-terminated byte string (<c>H5MM_xstrdup</c> in
/// <c>H5Odtype.c</c>), so real files carry UTF-8 names — h5py writes <c>"µA"</c> as
/// <c>c2 b5 41</c> and both h5py and h5dump read it back intact. Encoding it as ASCII replaces
/// every byte >= 0x80 with <c>'?'</c>, destroying the name before any reader is involved.
/// <para>These assertions go through h5dump rather than PureHDF's own reader, so they check
/// what actually reached the file.</para>
/// </summary>
[Collection(SharedHdf5StateCollection.Name)]
public class NonAsciiNameWritingTests
{
    [Fact]
    public void CanWrite_CompoundMemberName_WithNonAsciiCharacters()
    {
        var file = new H5File
        {
            ["compound"] = new Row[] { new() { First = 1.0, Second = 2, Third = 3 } }
        };

        var dump = Dump(file, Mapper);

        Assert.Contains("µA", dump);
        Assert.Contains("°C", dump);
        Assert.Contains("→", dump);
        Assert.DoesNotContain("?", dump);
    }

    [Fact]
    public void CanWrite_EnumerationMemberName_WithNonAsciiCharacters()
    {
        var file = new H5File
        {
            ["enumeration"] = new[] { Unit.µA }
        };

        var dump = Dump(file, fieldNameMapper: null);

        Assert.Contains("µA", dump);
        Assert.DoesNotContain("?", dump);
    }

    [Fact]
    public void CanWrite_OpaqueTag_WithNonAsciiCharacters()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var file = new H5File
        {
            ["opaque"] = new H5Dataset(data, opaqueInfo: new H5OpaqueInfo((uint)data.Length, "µA"))
        };

        var dump = Dump(file, fieldNameMapper: null);

        Assert.Contains("µA", dump);
        Assert.DoesNotContain("?", dump);
    }

    // Each of these names is one byte longer than its character count, so a size computed
    // from the character count under-declares the datatype message and the members that
    // follow are misplaced. h5dump then reports a malformed file rather than agreeing.
    private static string? Mapper(FieldInfo fieldInfo) => fieldInfo.Name switch
    {
        nameof(Row.First) => "µA",
        nameof(Row.Second) => "°C",
        nameof(Row.Third) => "→",
        _ => null
    };

    private static string Dump(H5File file, Func<FieldInfo, string?>? fieldNameMapper)
    {
        var filePath = Path.GetTempFileName();

        try
        {
            file.Write(filePath, new H5WriteOptions(FieldNameMapper: fieldNameMapper!));

            return TestUtils.DumpH5File(filePath)
                ?? throw new Exception("h5dump produced no output.");
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Row
    {
        public double First;
        public int Second;
        public short Third;
    }

    private enum Unit
    {
        µA = 0
    }
}
