using System.Buffers.Binary;
using System.Runtime.InteropServices;
using HDF.PInvoke;

namespace PureHDF.Tests;

public partial class TestUtils
{
    /// <summary>
    /// Big-endian data in every place a byte order can hide: on its own, as a compound member, mixed
    /// with a little-endian member, and under an enumeration.
    /// </summary>
    /// <remarks>
    /// The memory type is the file type here, so the buffers below are already in the file's byte
    /// order and the C library copies them through rather than converting. That is deliberate - the
    /// point is to produce the exact bytes a big-endian producer would.
    /// </remarks>
    public static unsafe void AddEndianness(long fileId, ContainerType container)
    {
        /* big-endian int32: 1, 2, 3 */
        var beInt = new byte[3 * sizeof(int)];

        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32BigEndian(beInt.AsSpan(i * sizeof(int)), i + 1);

        fixed (byte* ptr = beInt)
            Add(container, fileId, "endianness", "be_i32", H5T.STD_I32BE, ptr, 3UL);

        /* little-endian int32 alongside it, so a regression in the common path shows up here too */
        var leInt = new byte[3 * sizeof(int)];

        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32LittleEndian(leInt.AsSpan(i * sizeof(int)), i + 1);

        fixed (byte* ptr = leInt)
            Add(container, fileId, "endianness", "le_i32", H5T.STD_I32LE, ptr, 3UL);

        /* big-endian float64: 1.5, 2.5 */
        var beDouble = new byte[2 * sizeof(double)];
        BinaryPrimitives.WriteDoubleBigEndian(beDouble.AsSpan(0), 1.5);
        BinaryPrimitives.WriteDoubleBigEndian(beDouble.AsSpan(sizeof(double)), 2.5);

        fixed (byte* ptr = beDouble)
            Add(container, fileId, "endianness", "be_f64", H5T.IEEE_F64BE, ptr, 2UL);

        /* compound whose members are both big-endian: (1, 2.0), (3, 4.0) */
        var compoundTypeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(sizeof(int) + sizeof(double)));
        _ = H5T.insert(compoundTypeId, "a", new IntPtr(0), H5T.STD_I32BE);
        _ = H5T.insert(compoundTypeId, "b", new IntPtr(sizeof(int)), H5T.IEEE_F64BE);

        var beCompound = new byte[2 * (sizeof(int) + sizeof(double))];
        BinaryPrimitives.WriteInt32BigEndian(beCompound.AsSpan(0), 1);
        BinaryPrimitives.WriteDoubleBigEndian(beCompound.AsSpan(4), 2.0);
        BinaryPrimitives.WriteInt32BigEndian(beCompound.AsSpan(12), 3);
        BinaryPrimitives.WriteDoubleBigEndian(beCompound.AsSpan(16), 4.0);

        fixed (byte* ptr = beCompound)
            Add(container, fileId, "endianness", "be_compound", compoundTypeId, ptr, 2UL);

        _ = H5T.close(compoundTypeId);

        /* one member of each order, which a single file-wide answer cannot get right */
        var mixedTypeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(2 * sizeof(int)));
        _ = H5T.insert(mixedTypeId, "le", new IntPtr(0), H5T.STD_I32LE);
        _ = H5T.insert(mixedTypeId, "be", new IntPtr(sizeof(int)), H5T.STD_I32BE);

        var mixed = new byte[2 * 2 * sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(mixed.AsSpan(0), 7);
        BinaryPrimitives.WriteInt32BigEndian(mixed.AsSpan(4), 7);
        BinaryPrimitives.WriteInt32LittleEndian(mixed.AsSpan(8), 8);
        BinaryPrimitives.WriteInt32BigEndian(mixed.AsSpan(12), 8);

        fixed (byte* ptr = mixed)
            Add(container, fileId, "endianness", "mixed_compound", mixedTypeId, ptr, 2UL);

        _ = H5T.close(mixedTypeId);

        /* enumeration over a big-endian base type: the order is one level down */
        var enumTypeId = H5T.enum_create(H5T.STD_I32BE);

        var zero = new byte[sizeof(int)];
        var one = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(zero, 0);
        BinaryPrimitives.WriteInt32BigEndian(one, 1);

        fixed (byte* zeroPtr = zero) _ = H5T.enum_insert(enumTypeId, "A", new IntPtr(zeroPtr));
        fixed (byte* onePtr = one) _ = H5T.enum_insert(enumTypeId, "B", new IntPtr(onePtr));

        var enumData = new byte[2 * sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(enumData.AsSpan(0), 0);
        BinaryPrimitives.WriteInt32BigEndian(enumData.AsSpan(sizeof(int)), 1);

        fixed (byte* ptr = enumData)
            Add(container, fileId, "endianness", "be_enum", enumTypeId, ptr, 2UL);

        _ = H5T.close(enumTypeId);

        /* an array datatype whose base type is big-endian: the order is below the array */
        var arrayBaseId = H5T.array_create(H5T.STD_I32BE, 1, [3UL]);
        var arrayTypeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(3 * sizeof(int)));
        _ = H5T.insert(arrayTypeId, "v", new IntPtr(0), arrayBaseId);

        var arrayData = new byte[3 * sizeof(int)];

        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32BigEndian(arrayData.AsSpan(i * sizeof(int)), i + 1);

        fixed (byte* ptr = arrayData)
            Add(container, fileId, "endianness", "be_array_member", arrayTypeId, ptr, 1UL);

        _ = H5T.close(arrayTypeId);
        _ = H5T.close(arrayBaseId);

        /* a compound member whose type is itself a compound: the inner member carries the order */
        var innerCompoundTypeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(sizeof(int)));
        _ = H5T.insert(innerCompoundTypeId, "x", new IntPtr(0), H5T.STD_I32BE);

        var outerCompoundTypeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(sizeof(int)));
        _ = H5T.insert(outerCompoundTypeId, "nested", new IntPtr(0), innerCompoundTypeId);

        var nestedCompoundData = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(nestedCompoundData, 42);

        fixed (byte* ptr = nestedCompoundData)
            Add(container, fileId, "endianness", "be_nested_compound_member", outerCompoundTypeId, ptr, 1UL);

        _ = H5T.close(outerCompoundTypeId);
        _ = H5T.close(innerCompoundTypeId);

        /* a variable-length sequence whose heap payload is big-endian */
        var vlenIntTypeId = H5T.vlen_create(H5T.STD_I32BE);
        var vlenPayload = new byte[3 * sizeof(int)];

        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32BigEndian(vlenPayload.AsSpan(i * sizeof(int)), i + 1);

        fixed (byte* payloadPtr = vlenPayload)
        {
            var hvl = new H5T.hvl_t { len = new IntPtr(3), p = new IntPtr(payloadPtr) };

            Add(container, fileId, "endianness", "be_vlen_i32", vlenIntTypeId, &hvl, 1UL);
        }

        /* a compound that stores the variable-length header inline, but its values in the heap */
        var vlenHeaderSize = Marshal.SizeOf<H5T.hvl_t>();
        var compoundVlenTypeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(sizeof(int) + vlenHeaderSize));
        _ = H5T.insert(compoundVlenTypeId, "id", new IntPtr(0), H5T.STD_I32BE);
        _ = H5T.insert(compoundVlenTypeId, "values", new IntPtr(sizeof(int)), vlenIntTypeId);

        var compoundVlenData = new byte[sizeof(int) + vlenHeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(compoundVlenData, 5);

        fixed (byte* payloadPtr = vlenPayload)
        {
            var hvl = new H5T.hvl_t { len = new IntPtr(3), p = new IntPtr(payloadPtr) };
            MemoryMarshal.Write(compoundVlenData.AsSpan(sizeof(int)), in hvl);

            fixed (byte* ptr = compoundVlenData)
                Add(container, fileId, "endianness", "be_compound_vlen_member", compoundVlenTypeId, ptr, 1UL);
        }

        _ = H5T.close(compoundVlenTypeId);
        _ = H5T.close(vlenIntTypeId);
    }
}
