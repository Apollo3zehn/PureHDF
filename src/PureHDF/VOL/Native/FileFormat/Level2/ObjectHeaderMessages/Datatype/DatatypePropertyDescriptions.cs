using System.Buffers;
using System.Text;

namespace PureHDF.VOL.Native;

internal abstract record class DatatypePropertyDescription
{
    public abstract void Encode(H5DriverBase driver, uint typeSize /* only for compound v3 */);

    public abstract ushort GetEncodeSize(uint typeSize /* only for compound v3 */);
};

internal record class ArrayPropertyDescription(
    byte Rank,
    uint[] DimensionSizes,
    uint[] PermutationIndices,
    DatatypeMessage BaseType)
    : DatatypePropertyDescription
{
    public static async ValueTask<ArrayPropertyDescription> Decode(
        H5DriverBase driver, byte version)
    {
        // rank
        var rank = await driver.ReadByte().ConfigureAwait(false);

        // reserved
        if (version == 2)
            await driver.ReadBytes(3).ConfigureAwait(false);

        // dimension sizes
        var dimensionSizes = new uint[rank];

        for (int i = 0; i < rank; i++)
        {
            dimensionSizes[i] = await driver.ReadUInt32().ConfigureAwait(false);
        }

        // permutation indices
        var permutationIndices = new uint[rank];

        if (version == 2)
        {
            for (int i = 0; i < rank; i++)
            {
                permutationIndices[i] = await driver.ReadUInt32().ConfigureAwait(false);
            }
        }

        // base type
        var baseType = await DatatypeMessage.Decode(driver).ConfigureAwait(false);

        return new ArrayPropertyDescription(
            Rank: rank,
            DimensionSizes: dimensionSizes,
            PermutationIndices: permutationIndices,
            BaseType: baseType
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        throw new NotImplementedException();
    }
}
internal record class BitFieldPropertyDescription(
    ushort BitOffset,
    ushort BitPrecision)
    : DatatypePropertyDescription
{
    public static async ValueTask<BitFieldPropertyDescription> Decode(
        H5DriverBase driver)
    {
        return new BitFieldPropertyDescription(
            BitOffset: await driver.ReadUInt16().ConfigureAwait(false),
            BitPrecision: await driver.ReadUInt16().ConfigureAwait(false)
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        return
            sizeof(uint) +
            sizeof(uint);
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        throw new NotImplementedException();
    }
}
internal record class CompoundPropertyDescription(
    string Name,
    ulong MemberByteOffset,
    DatatypeMessage MemberTypeMessage)
    : DatatypePropertyDescription
{
    public static async ValueTask<CompoundPropertyDescription> Decode(
        H5DriverBase driver,
        byte version,
        uint valueSize)
    {
        string name;
        ulong memberByteOffset;
        DatatypeMessage memberTypeMessage;

        switch (version)
        {
            case 1:

                // name
                name = await ReadUtils.ReadNullTerminatedString(driver, pad: true).ConfigureAwait(false);

                // member byte offset
                memberByteOffset = await driver.ReadUInt32().ConfigureAwait(false);

                // rank
                _ = await driver.ReadByte().ConfigureAwait(false);

                // padding bytes
                await driver.ReadBytes(3).ConfigureAwait(false);

                // dimension permutation
                _ = await driver.ReadUInt32().ConfigureAwait(false);

                // padding byte
                await driver.ReadBytes(4).ConfigureAwait(false);

                // dimension sizes
                var dimensionSizes = new uint[4];

                for (int i = 0; i < 4; i++)
                {
                    dimensionSizes[i] = await driver.ReadUInt32().ConfigureAwait(false);
                }

                // member type message
                memberTypeMessage = await DatatypeMessage.Decode(driver).ConfigureAwait(false);

                break;

            case 2:

                // name
                name = await ReadUtils.ReadNullTerminatedString(driver, pad: true).ConfigureAwait(false);

                // member byte offset
                memberByteOffset = await driver.ReadUInt32().ConfigureAwait(false);

                // member type message
                memberTypeMessage = await DatatypeMessage.Decode(driver).ConfigureAwait(false);

                break;

            case 3:

                // name
                name = await ReadUtils.ReadNullTerminatedString(driver, pad: false).ConfigureAwait(false);

                // member byte offset
                var byteCount = MathUtils.FindMinByteCount(valueSize);

                if (!(1 <= byteCount && byteCount <= 8))
                    throw new NotSupportedException("A compound property description member byte offset byte count must be within the range of 1..8.");

                using (var byteOffsetOwner = new ScratchBuffer<byte>(8))
                {
                    var buffer = byteOffsetOwner.Memory[..8];

                    // This was `stackalloc byte[8]`, which the runtime zero-initialises. A pooled
                    // buffer is recycled and arbitrary, and only `byteCount` of the 8 bytes are
                    // written below, so the remainder must be cleared or the high bytes of the
                    // offset are garbage.
                    buffer.Span.Clear();

                    for (int i = 0; i < (int)byteCount; i++)
                    {
                        var b = await driver.ReadByte().ConfigureAwait(false);
                        buffer.Span[i] = b;
                    }

                    memberByteOffset = BitConverter.ToUInt64(buffer.Span);
                }

                // member type message
                memberTypeMessage = await DatatypeMessage.Decode(driver).ConfigureAwait(false);

                break;

            default:
                throw new Exception("The version parameter must be in the range 1..3.");
        }

        return new CompoundPropertyDescription(
            Name: name,
            MemberByteOffset: memberByteOffset,
            MemberTypeMessage: memberTypeMessage
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        var nameBytesCount = Encoding.UTF8.GetByteCount(Name) + 1;
        var byteCount = MathUtils.FindMinByteCount(typeSize);

        var encodeSize =
            (ulong)nameBytesCount +
            byteCount +
            MemberTypeMessage.GetEncodeSize();

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        // The specification gives a compound member name no character set field, and the
        // reference library copies it as an opaque NUL-terminated byte string
        // (H5MM_xstrdup in H5Odtype.c), so whatever bytes the caller supplied are stored.
        // Real files therefore carry UTF-8 names: h5py writes "µA" as c2 b5 41 and both
        // h5py and h5dump read it back intact. GetEncodeSize above counts the same bytes.
        var nameBytes = Encoding.UTF8.GetBytes(Name);
        driver.Write(nameBytes);
        driver.Write((byte)0);

        // member byte offset
        var byteCount = MathUtils.FindMinByteCount(typeSize);

        if (!(1 <= byteCount && byteCount <= 8))
            throw new NotSupportedException("A compound property description member byte offset byte count must be within the range of 1..8.");

        var memberByteOffsetBytes = BitConverter.GetBytes(MemberByteOffset);
        var slicedMemberByteOffsetBytes = memberByteOffsetBytes.AsSpan(0, (int)byteCount);

        driver.Write(slicedMemberByteOffsetBytes);

        // member type message
        MemberTypeMessage.Encode(driver);
    }
}
internal record class EnumerationPropertyDescription(
    DatatypeMessage BaseType,
    string[] Names,
    byte[][] Values)
    : DatatypePropertyDescription
{
    public static async ValueTask<EnumerationPropertyDescription> Decode(
        H5DriverBase driver,
        byte version,
        uint valueSize,
        ushort memberCount)
    {
        // base type
        var baseType = await DatatypeMessage.Decode(driver).ConfigureAwait(false);

        // names
        var names = new string[memberCount];

        for (int i = 0; i < memberCount; i++)
        {
            names[i] = await ReadUtils.ReadNullTerminatedString(driver, pad: version <= 2).ConfigureAwait(false);
        }

        // values
        var values = new byte[memberCount][];

        for (int i = 0; i < memberCount; i++)
        {
            values[i] = await driver.ReadBytes((int)valueSize).ConfigureAwait(false);
        }

        return new EnumerationPropertyDescription(
            BaseType: baseType,
            Names: names,
            Values: values
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        var encodeSize =
            BaseType.GetEncodeSize() +
            Names.Aggregate(0, (sum, name) => sum + Encoding.UTF8.GetByteCount(name) + 1) +
            Values.Aggregate(0, (sum, value) => sum + value.Length);

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        // base type
        BaseType.Encode(driver);

        // names
        // The specification calls these ASCII, but the reference library stores them as
        // opaque byte strings like compound member names and h5py round-trips UTF-8 through
        // them, so encoding as ASCII would replace a caller's non-ASCII name with '?' while
        // no reader requires it.
        foreach (var name in Names)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            driver.Write(nameBytes);
            driver.Write((byte)0);
        }

        // values
        foreach (var value in Values)
        {
            driver.Write(value);
        }
    }
};

internal record class FixedPointPropertyDescription(
    ushort BitOffset,
    ushort BitPrecision)
    : DatatypePropertyDescription
{
    public static async ValueTask<FixedPointPropertyDescription> Decode(
        H5DriverBase driver)
    {
        return new FixedPointPropertyDescription(
            BitOffset: await driver.ReadUInt16().ConfigureAwait(false),
            BitPrecision: await driver.ReadUInt16().ConfigureAwait(false)
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        return
            sizeof(ushort) +
            sizeof(ushort);
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        driver.Write(BitOffset);
        driver.Write(BitPrecision);
    }
};

internal record class FloatingPointPropertyDescription(
    ushort BitOffset,
    ushort BitPrecision,
    byte ExponentLocation,
    byte ExponentSize,
    byte MantissaLocation,
    byte MantissaSize,
    uint ExponentBias)
    : DatatypePropertyDescription
{
    public static async ValueTask<FloatingPointPropertyDescription> Decode(
        H5DriverBase driver)
    {
        return new FloatingPointPropertyDescription(
            BitOffset: await driver.ReadUInt16().ConfigureAwait(false),
            BitPrecision: await driver.ReadUInt16().ConfigureAwait(false),
            ExponentLocation: await driver.ReadByte().ConfigureAwait(false),
            ExponentSize: await driver.ReadByte().ConfigureAwait(false),
            MantissaLocation: await driver.ReadByte().ConfigureAwait(false),
            MantissaSize: await driver.ReadByte().ConfigureAwait(false),
            ExponentBias: await driver.ReadUInt32().ConfigureAwait(false)
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        return
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(uint);
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        driver.Write(BitOffset);
        driver.Write(BitPrecision);
        driver.Write(ExponentLocation);
        driver.Write(ExponentSize);
        driver.Write(MantissaLocation);
        driver.Write(MantissaSize);
        driver.Write(ExponentBias);
    }
};

internal record class OpaquePropertyDescription(
    string Tag)
    : DatatypePropertyDescription
{
    public static async ValueTask<OpaquePropertyDescription> Decode(
        H5DriverBase driver,
        byte tagByteLength)
    {
        return new OpaquePropertyDescription(
            Tag: (await ReadUtils
                .ReadFixedLengthString(driver, tagByteLength)
                .ConfigureAwait(false))
                .TrimEnd('\0')
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        // Counted in bytes, since that is what the limit and the padding are measured in.
        var unpaddedLength = Encoding.UTF8.GetByteCount(Tag) + 1;

        if (unpaddedLength > byte.MaxValue)
            throw new Exception($"The maximum opaque tag length is {byte.MaxValue - 1} bytes");

        return (ushort)MathUtils.Ceil_N(unpaddedLength, 8);
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        var bytes = Encoding.UTF8.GetBytes(Tag);
        var length = bytes.Length + 1;
        var padBytesCount = MathUtils.Ceil_N(length, 8) - length;

        driver.Write(bytes);
        driver.Write((byte)0);

        Span<byte> padBytes = stackalloc byte[padBytesCount];
        driver.Write(padBytes);
    }
}

internal record class TimePropertyDescription(
    ushort BitPrecision)
    : DatatypePropertyDescription
{
    public static async ValueTask<TimePropertyDescription> Decode(
        H5DriverBase driver)
    {
        return new TimePropertyDescription(
            BitPrecision: await driver.ReadUInt16().ConfigureAwait(false)
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        throw new NotImplementedException();
    }
}

internal record class VariableLengthPropertyDescription(
    DatatypeMessage BaseType)
    : DatatypePropertyDescription
{
    public static async ValueTask<VariableLengthPropertyDescription> Decode(
        H5DriverBase driver)
    {
        return new VariableLengthPropertyDescription(
            BaseType: await DatatypeMessage.Decode(driver).ConfigureAwait(false)
        );
    }

    public override ushort GetEncodeSize(uint typeSize)
    {
        return BaseType.GetEncodeSize();
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        BaseType.Encode(driver);
    }
};