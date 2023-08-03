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
    public static ArrayPropertyDescription Decode(
        H5DriverBase driver, byte version)
    {
        // rank
        var rank = driver.ReadByte();

        // reserved
        if (version == 2)
            driver.ReadBytes(3);

        // dimension sizes
        var dimensionSizes = new uint[rank];

        for (int i = 0; i < rank; i++)
        {
            dimensionSizes[i] = driver.ReadUInt32();
        }

        // permutation indices
        var permutationIndices = new uint[rank];

        if (version == 2)
        {
            for (int i = 0; i < rank; i++)
            {
                permutationIndices[i] = driver.ReadUInt32();
            }
        }

        // base type
        var baseType = DatatypeMessage.Decode(driver);

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
    public static BitFieldPropertyDescription Decode(
        H5DriverBase driver)
    {
        return new BitFieldPropertyDescription(
            BitOffset: driver.ReadUInt16(),
            BitPrecision: driver.ReadUInt16()
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
    public static CompoundPropertyDescription Decode(
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
                name = ReadUtils.ReadNullTerminatedString(driver, pad: true);

                // member byte offset
                memberByteOffset = driver.ReadUInt32();

                // rank
                _ = driver.ReadByte();

                // padding bytes
                driver.ReadBytes(3);

                // dimension permutation
                _ = driver.ReadUInt32();

                // padding byte
                driver.ReadBytes(4);

                // dimension sizes
                var dimensionSizes = new uint[4];

                for (int i = 0; i < 4; i++)
                {
                    dimensionSizes[i] = driver.ReadUInt32();
                }

                // member type message
                memberTypeMessage = DatatypeMessage.Decode(driver);

                break;

            case 2:

                // name
                name = ReadUtils.ReadNullTerminatedString(driver, pad: true);

                // member byte offset
                memberByteOffset = driver.ReadUInt32();

                // member type message
                memberTypeMessage = DatatypeMessage.Decode(driver);

                break;

            case 3:

                // name
                name = ReadUtils.ReadNullTerminatedString(driver, pad: false);

                // member byte offset
                var byteCount = Utils.FindMinByteCount(valueSize);

                if (!(1 <= byteCount && byteCount <= 8))
                    throw new NotSupportedException("A compound property description member byte offset byte count must be within the range of 1..8.");

                var buffer = new byte[8];

                for (ulong i = 0; i < byteCount; i++)
                {
                    buffer[i] = driver.ReadByte();
                }

                memberByteOffset = BitConverter.ToUInt64(buffer, 0);

                // member type message
                memberTypeMessage = DatatypeMessage.Decode(driver);

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
        // TODO is this really ASCII? The spec does not specify it but does it so for enumerated data type (there it is ASCII)
        var nameBytesCount = Name.Length + 1;
        var byteCount = Utils.FindMinByteCount(typeSize);

        var encodeSize =
            (ulong)nameBytesCount +
            byteCount +
            MemberTypeMessage.GetEncodeSize();

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        // name
        // TODO is this really ASCII? The spec does not specify it but does it so for enumerated data type (there it is ASCII)
        var nameBytes = Encoding.ASCII.GetBytes(Name);
        driver.Write(nameBytes);
        driver.Write((byte)0);

        // member byte offset
        var byteCount = Utils.FindMinByteCount(typeSize);

        if (!(1 <= byteCount && byteCount <= 8))
            throw new NotSupportedException("A compound property description member byte offset byte count must be within the range of 1..8.");

        var memberByteOffsetBytes = BitConverter.GetBytes(MemberByteOffset);
        var slicedMemberByteOffsetBytes = memberByteOffsetBytes.AsSpan(0, (int)byteCount);

#if NETSTANDARD2_1_OR_GREATER
        driver.Write(slicedMemberByteOffsetBytes);
#else
        driver.Write(slicedMemberByteOffsetBytes.ToArray());
#endif
    
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
    public static EnumerationPropertyDescription Decode(
        H5DriverBase driver, 
        byte version, 
        uint valueSize, 
        ushort memberCount)
    {
        // base type
        var baseType = DatatypeMessage.Decode(driver);

        // names
        var names = new string[memberCount];

        for (int i = 0; i < memberCount; i++)
        {
            names[i] = ReadUtils.ReadNullTerminatedString(driver, pad: version <= 2);
        }

        // values
        var values = new byte[memberCount][];

        for (int i = 0; i < memberCount; i++)
        {
            values[i] = driver.ReadBytes((int)valueSize);
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
            Names.Aggregate(0, (sum, name) => sum + name.Length + 1) +
            Values.Aggregate(0, (sum, value) => sum + value.Length);

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver, uint typeSize)
    {
        // base type
        BaseType.Encode(driver);

        // names
        foreach (var name in Names)
        {
            var nameBytes = Encoding.ASCII.GetBytes(name);
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
    public static FixedPointPropertyDescription Decode(
        H5DriverBase driver)
    {
        return new FixedPointPropertyDescription(
            BitOffset: driver.ReadUInt16(),
            BitPrecision: driver.ReadUInt16()
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
    public static FloatingPointPropertyDescription Decode(
        H5DriverBase driver)
    {
        return new FloatingPointPropertyDescription(
            BitOffset: driver.ReadUInt16(),
            BitPrecision: driver.ReadUInt16(),
            ExponentLocation: driver.ReadByte(),
            ExponentSize: driver.ReadByte(),
            MantissaLocation: driver.ReadByte(), 
            MantissaSize: driver.ReadByte(),
            ExponentBias: driver.ReadUInt32()
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
    public static OpaquePropertyDescription Decode(
        H5DriverBase driver, 
        byte tagByteLength)
    {
        return new OpaquePropertyDescription(
            Tag: ReadUtils
                .ReadFixedLengthString(driver, tagByteLength)
                .TrimEnd('\0')
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

internal record class TimePropertyDescription(
    ushort BitPrecision)
    : DatatypePropertyDescription
{
    public static TimePropertyDescription Decode(
        H5DriverBase driver)
    {
        return new TimePropertyDescription(
            BitPrecision: driver.ReadUInt16()
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
    public static VariableLengthPropertyDescription Decode(
        H5DriverBase driver)
    {
        return new VariableLengthPropertyDescription(
            BaseType: DatatypeMessage.Decode(driver)
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