using System.Text;

namespace PureHDF.VOL.Native;

internal abstract record class DatatypePropertyDescription
{
    public virtual void Encode(BinaryWriter driver)
    {
        //
    }
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
};

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
};

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
};

internal record class EnumerationPropertyDescription(
    DatatypeMessage BaseType,
    string[] Names,
    byte[][] Values)
    : DatatypePropertyDescription
{
    public static EnumerationPropertyDescription Create(Type type, int typeSize)
    {
        var underlyingType = Enum.GetUnderlyingType(type);
        var enumValues = Enum.GetValues(type);
        var enumObjects = new object[enumValues.Length];

        for (int i = 0; i < enumValues.Length; i++)
        {
            enumObjects[i] = enumValues.GetValue(i)!;
        }

        var values = (underlyingType switch
        {
            Type t when t == typeof(byte) => enumObjects.Select(enumValue => BitConverter.GetBytes((byte)enumValue)),
            Type t when t == typeof(sbyte) => enumObjects.Select(enumValue => BitConverter.GetBytes((sbyte)enumValue)),
            Type t when t == typeof(ushort) => enumObjects.Select(enumValue => BitConverter.GetBytes((ushort)enumValue)),
            Type t when t == typeof(short) => enumObjects.Select(enumValue => BitConverter.GetBytes((short)enumValue)),
            Type t when t == typeof(uint) => enumObjects.Select(enumValue => BitConverter.GetBytes((uint)enumValue)),
            Type t when t == typeof(int) => enumObjects.Select(enumValue => BitConverter.GetBytes((int)enumValue)),
            Type t when t == typeof(ulong) => enumObjects.Select(enumValue => BitConverter.GetBytes((ulong)enumValue)),
            Type t when t == typeof(long) => enumObjects.Select(enumValue => BitConverter.GetBytes((long)enumValue)),
            _ => throw new Exception($"The enum type {underlyingType} is not supported.")
        }).ToArray();

        return new EnumerationPropertyDescription(
            BaseType: DatatypeMessage.Create(underlyingType, typeSize),
            Names: Enum.GetNames(type),
            Values: values
        );
    }

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

    public override void Encode(BinaryWriter driver)
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

    public override void Encode(BinaryWriter driver)
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

    public override void Encode(BinaryWriter driver)
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
};

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
};

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
};