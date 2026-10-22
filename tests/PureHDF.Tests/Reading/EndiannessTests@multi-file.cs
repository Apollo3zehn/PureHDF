using System.Buffers.Binary;
using System.Runtime.InteropServices;
using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Verifies that byte-order decoder caching is scoped by file datatype, not just by CLR type.
/// </summary>
/// <remarks>
/// The two files contain the same datasets and are read into the same .NET types, but one file stores
/// those values big-endian and the other little-endian. Reading either file alone would not prove that
/// a cached decoder still uses the byte order of the file it is decoding now. The files are therefore
/// opened in both orders and revisited, so a decoder cache keyed only by CLR type would reuse the first
/// file's conversion plan and corrupt reads from the other file.
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public unsafe class MultiFileEndiannessTests
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Compound { public int a; public double b; }

    private class CompoundClass { public int a { get; set; } public double b { get; set; } }

    /// <summary>Writes the identical structure in the requested byte order.</summary>
    private static unsafe void AddShapes(long fileId, bool bigEndian)
    {
        var intType = bigEndian ? H5T.STD_I32BE : H5T.STD_I32LE;
        var doubleType = bigEndian ? H5T.IEEE_F64BE : H5T.IEEE_F64LE;

        void WriteInt(Span<byte> target, int value)
        {
            if (bigEndian) BinaryPrimitives.WriteInt32BigEndian(target, value);
            else BinaryPrimitives.WriteInt32LittleEndian(target, value);
        }

        void WriteDouble(Span<byte> target, double value)
        {
            if (bigEndian) BinaryPrimitives.WriteDoubleBigEndian(target, value);
            else BinaryPrimitives.WriteDoubleLittleEndian(target, value);
        }

        var ints = new byte[3 * sizeof(int)];

        for (var i = 0; i < 3; i++)
            WriteInt(ints.AsSpan(i * sizeof(int)), i + 1);

        fixed (byte* ptr = ints)
            TestUtils.Add(ContainerType.Dataset, fileId, "shapes", "ints", intType, ptr, 3UL);

        var compoundTypeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(sizeof(int) + sizeof(double)));
        _ = H5T.insert(compoundTypeId, "a", new IntPtr(0), intType);
        _ = H5T.insert(compoundTypeId, "b", new IntPtr(sizeof(int)), doubleType);

        var compound = new byte[sizeof(int) + sizeof(double)];
        WriteInt(compound.AsSpan(0), 1);
        WriteDouble(compound.AsSpan(sizeof(int)), 2.0);

        fixed (byte* ptr = compound)
            TestUtils.Add(ContainerType.Dataset, fileId, "shapes", "compound", compoundTypeId, ptr, 1UL);

        _ = H5T.close(compoundTypeId);

        /* a genuine SCALAR dataspace, read into a bare int rather than an array */
        var scalar = new byte[sizeof(int)];
        WriteInt(scalar, 42);
        var scalarSpaceId = H5S.create(H5S.class_t.SCALAR);

        fixed (byte* ptr = scalar)
            TestUtils.Add(ContainerType.Dataset, fileId, "shapes", "scalar", intType, ptr, scalarSpaceId);

        _ = H5S.close(scalarSpaceId);

        /* a variable-length sequence: its values live on the global heap, decoded by yet another path */
        var vlenTypeId = H5T.vlen_create(intType);
        var payload = new byte[3 * sizeof(int)];

        for (var i = 0; i < 3; i++)
            WriteInt(payload.AsSpan(i * sizeof(int)), i + 1);

        fixed (byte* payloadPtr = payload)
        {
            var hvl = new H5T.hvl_t { len = new IntPtr(3), p = new IntPtr(payloadPtr) };

            TestUtils.Add(ContainerType.Dataset, fileId, "shapes", "vlen", vlenTypeId, &hvl, 1UL);
        }

        _ = H5T.close(vlenTypeId);
    }

    private static string CreateFile(bool bigEndian)
        => TestUtils.PrepareTestFile(H5F.libver_t.LATEST, fileId => AddShapes(fileId, bigEndian));

    /// <summary>Every shape in one file, checked against the values both files were written with.</summary>
    private static void AssertReadsCorrectly(string path, string label)
    {
        using var root = NativeFile.InternalOpenRead(path);
        var group = root.Group("shapes");

        Assert.Equal<int>([1, 2, 3], group.Dataset("ints").Read<int[]>());

        var asStruct = Assert.Single(group.Dataset("compound").Read<Compound[]>());
        Assert.Equal(1, asStruct.a);
        Assert.Equal(2.0, asStruct.b);

        var asClass = Assert.Single(group.Dataset("compound").Read<CompoundClass[]>());
        Assert.Equal(1, asClass.a);
        Assert.Equal(2.0, asClass.b);

        Assert.Equal(42, group.Dataset("scalar").Read<int>());
        Assert.Equal<int>([1, 2, 3], Assert.Single(group.Dataset("vlen").Read<int[][]>()));
    }

    [Theory]
    [InlineData(true)]   // big-endian file opened first
    [InlineData(false)]  // little-endian file opened first
    public void FilesOfOppositeByteOrderDoNotAffectEachOther(bool bigEndianFirst)
    {
        var bigEndianPath = CreateFile(bigEndian: true);
        var littleEndianPath = CreateFile(bigEndian: false);

        try
        {
            string[] order = bigEndianFirst
                ? [bigEndianPath, littleEndianPath, bigEndianPath, littleEndianPath]
                : [littleEndianPath, bigEndianPath, littleEndianPath, bigEndianPath];

            // Revisited, not just interleaved: a decoder cached on the first pass has to still be
            // right for the file it was not built from, and then right again for the one it was.
            for (var i = 0; i < order.Length; i++)
            {
                AssertReadsCorrectly(order[i], $"pass {i}");
            }
        }

        finally
        {
            foreach (var path in new[] { bigEndianPath, littleEndianPath })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
