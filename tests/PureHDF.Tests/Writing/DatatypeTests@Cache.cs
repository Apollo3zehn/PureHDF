using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
/// The write-side datatype cache is keyed by everything a message depends on, not by
/// <see cref="Type"/> alone. A string's width and an opaque type's size and tag come from the
/// caller, so one type maps to many messages; keying only by type let one instance's width be
/// handed to another's.
/// </summary>
public class DatatypeCacheTests
{
    private const string Long = "a-value-that-is-much-longer-than-three-characters";

    /// <summary>
    /// A fixed-width string compound member and a variable-length string attribute are both
    /// <c>typeof(string)</c>. Encoding order matters: the attribute on the group before the
    /// dataset is written first and was always correct, so the defect hid behind it.
    /// </summary>
    [Fact]
    public void CanWrite_StringAttribute_AfterANarrowFixedLengthStringMember()
    {
        var before = new H5Group();
        before.Attributes["text"] = Long;

        var after = new H5Group();
        after.Attributes["text"] = Long;

        var file = new H5File
        {
            ["a_before"] = before,
            ["b_table"] = new H5Group { ["table"] = new Row[] { new() { Unit = "Ohm" } } },
            ["c_after"] = after,
        };

        var (actualBefore, actualAfter) = RoundTrip(
            file,
            new H5WriteOptions(FieldStringLengthMapper: _ => 3),
            f => (f.Group("a_before").Attribute("text").Read<string>(),
                  f.Group("c_after").Attribute("text").Read<string>()));

        Assert.Equal(Long, actualBefore);
        Assert.Equal(Long, actualAfter);
    }

    /// <summary>Two widths for one type must not collapse into one message.</summary>
    [Fact]
    public void CanWrite_TwoFixedLengthStringMembers_OfDifferentWidths()
    {
        var file = new H5File
        {
            ["table"] = new WideRow[] { new() { Narrow = "abc", Wide = Long } }
        };

        static int? mapper(FieldInfo fieldInfo) =>
            fieldInfo.Name == nameof(WideRow.Narrow) ? 3 : Long.Length;

        var actual = RoundTrip(
            file,
            new H5WriteOptions(FieldStringLengthMapper: mapper),
            f => f.Dataset("table").Read<WideRow[]>()[0]);

        Assert.Equal("abc", actual.Narrow);
        Assert.Equal(Long, actual.Wide);
    }

    /// <summary>
    /// Opaque messages carry a size and a tag, so two opaque datasets differing in either must
    /// not share a cache entry.
    /// </summary>
    [Fact]
    public void CanWrite_TwoOpaqueDatasets_OfDifferentSizesAndTags()
    {
        var two = new byte[] { 1, 2 };
        var three = new byte[] { 1, 2, 3 };

        var file = new H5File
        {
            ["first"] = new H5Dataset(two, opaqueInfo: new H5OpaqueInfo((uint)two.Length, "TagA")),
            ["second"] = new H5Dataset(three, opaqueInfo: new H5OpaqueInfo((uint)three.Length, "TagB")),
        };

        var (firstSize, secondSize) = RoundTrip(
            file,
            new H5WriteOptions(),
            f => (f.Dataset("first").Type.Size, f.Dataset("second").Type.Size));

        Assert.Equal(two.Length, firstSize);
        Assert.Equal(three.Length, secondSize);
    }

    private static T RoundTrip<T>(H5File file, H5WriteOptions options, Func<IH5Group, T> act)
    {
        var memoryStream = new MemoryStream();
        file.Write(memoryStream, options);
        memoryStream.Seek(0, SeekOrigin.Begin);

        return act(H5File.Open(memoryStream));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Row
    {
        public string Unit;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WideRow
    {
        public string Narrow;
        public string Wide;
    }
}
