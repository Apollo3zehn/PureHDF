using System.Runtime.InteropServices;
using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Data whose byte order is not the host's is converted on read rather than refused or mangled.
/// </summary>
/// <remarks>
/// Big-endian is HDF5's portable choice and what a good deal of instrument software produces, so
/// refusing it put those files out of reach entirely. The refusal only ever inspected the TOP-LEVEL
/// datatype, though, which left every nested one unguarded: a compound with big-endian members was
/// byte-copied straight into the caller's struct and read back as plausible-looking nonsense.
/// <para>
/// The byte order is a property of each leaf, not of the file, which is what <c>mixed_compound</c>
/// is for - no single answer per dataset can be right for it.
/// </para>
/// </remarks>
// Builds its fixtures through the HDF5 C library, so it serialises with the other classes that
// touch that process-global state.
[Collection(SharedHdf5StateCollection.Name)]
public class EndiannessTests
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Compound { public int a; public double b; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Mixed { public int le; public int be; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct NestedCompoundMember { public int x; }

    private class CompoundClass { public int a { get; set; } public double b { get; set; } }

    private class ArrayMemberClass { public int[] v { get; set; } = default!; }

    private class NestedCompoundClass { public NestedCompoundMember nested { get; set; } }

    private class MixedClass { public int le { get; set; } public int be { get; set; } }

    private static NativeFile OpenFixture()
    {
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, fileId
            => TestUtils.AddEndianness(fileId, ContainerType.Dataset));

        return NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
    }

    [Fact]
    public void ABigEndianIntegerIsConverted()
    {
        using var root = OpenFixture();

        Assert.Equal<int>([1, 2, 3], root.Group("endianness").Dataset("be_i32").Read<int[]>());
    }

    [Fact]
    public void ALittleEndianIntegerIsUnaffected()
    {
        using var root = OpenFixture();

        Assert.Equal<int>([1, 2, 3], root.Group("endianness").Dataset("le_i32").Read<int[]>());
    }

    [Fact]
    public void ABigEndianFloatIsConverted()
    {
        using var root = OpenFixture();

        Assert.Equal<double>([1.5, 2.5], root.Group("endianness").Dataset("be_f64").Read<double[]>());
    }

    /// <summary>The case that produced silent nonsense: the order is on the members, not the compound.</summary>
    [Fact]
    public void BigEndianCompoundMembersAreConverted()
    {
        using var root = OpenFixture();

        var actual = root.Group("endianness").Dataset("be_compound").Read<Compound[]>();

        Assert.Equal(2, actual.Length);
        Assert.Equal(1, actual[0].a);
        Assert.Equal(2.0, actual[0].b);
        Assert.Equal(3, actual[1].a);
        Assert.Equal(4.0, actual[1].b);
    }

    /// <summary>Each member is converted on its own, so one of each order in one struct still works.</summary>
    [Fact]
    public void AMixedEndiannessCompoundIsConvertedPerMember()
    {
        using var root = OpenFixture();

        var actual = root.Group("endianness").Dataset("mixed_compound").Read<Mixed[]>();

        Assert.Equal(2, actual.Length);
        Assert.Equal(7, actual[0].le);
        Assert.Equal(7, actual[0].be);
        Assert.Equal(8, actual[1].le);
        Assert.Equal(8, actual[1].be);
    }

    /// <summary>
    /// The same compound read into a CLASS rather than a struct, which decodes by a different route.
    /// </summary>
    /// <remarks>
    /// A struct is bulk-copied and converted as a layout; a class is built member by member, and each
    /// member is converted by the decoder for its own leaf. Both routes have to agree, and only one of
    /// them is exercised by the struct test above.
    /// </remarks>
    [Fact]
    public void BigEndianCompoundMembersAreConvertedWhenReadAsAClass()
    {
        using var root = OpenFixture();

        var actual = root.Group("endianness").Dataset("be_compound").Read<CompoundClass[]>();

        Assert.Equal(2, actual.Length);
        Assert.Equal(1, actual[0].a);
        Assert.Equal(2.0, actual[0].b);
    }

    /// <summary>
    /// Two members of the SAME .NET type and opposite byte orders, in one element, read as a class.
    /// </summary>
    /// <remarks>
    /// This pins the decoder cache key rather than the conversion. The per-element decoders are cached
    /// process-wide, and keying that cache on the .NET type alone is not enough once a decoder converts:
    /// both members here are <c>int</c>, so one cache entry would have to serve both orders and whichever
    /// member was decoded first would decide for the other.
    /// <para>
    /// Reading the same dataset into a struct does not cover this - that goes through the bulk path,
    /// which converts by layout and never consults the per-element cache. The failure is otherwise
    /// invisible until some unrelated read of the same .NET type happens to go second, which makes it
    /// depend on the order files are opened in rather than on anything in the file.
    /// </para>
    /// </remarks>
    [Fact]
    public void MembersOfOneTypeAndOppositeOrdersDoNotShareADecoder()
    {
        using var root = OpenFixture();

        var actual = root.Group("endianness").Dataset("mixed_compound").Read<MixedClass[]>();

        Assert.Equal(2, actual.Length);
        Assert.Equal(7, actual[0].le);
        Assert.Equal(7, actual[0].be);
        Assert.Equal(8, actual[1].le);
        Assert.Equal(8, actual[1].be);
    }

    /// <summary>An array datatype carries its order below the array, on the base type.</summary>
    [Fact]
    public void ABigEndianArrayMemberIsConverted()
    {
        using var root = OpenFixture();

        var actual = root.Group("endianness").Dataset("be_array_member").Read<ArrayMemberClass[]>();

        Assert.Equal([1, 2, 3], Assert.Single(actual).v);
    }

    /// <summary>A nested compound member still has to be converted by its leaf member layout.</summary>
    [Fact]
    public void ABigEndianNestedCompoundMemberIsConverted()
    {
        using var root = OpenFixture();

        var actual = root.Group("endianness").Dataset("be_nested_compound_member").Read<NestedCompoundClass[]>();

        Assert.Equal(42, Assert.Single(actual).nested.x);
    }

    /// <summary>An enumeration carries its order on its base type, one level down.</summary>
    [Fact]
    public void ABigEndianEnumBaseTypeIsConverted()
    {
        using var root = OpenFixture();

        Assert.Equal([0, 1], root.Group("endianness").Dataset("be_enum").Read<int[]>());
    }
}
