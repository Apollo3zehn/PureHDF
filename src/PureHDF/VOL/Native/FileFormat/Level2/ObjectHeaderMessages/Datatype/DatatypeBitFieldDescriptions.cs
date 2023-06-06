namespace PureHDF.VOL.Native;

internal abstract record class DatatypeBitFieldDescription(
    //
);

internal record class ArrayBitFieldDescription(
    //
) : DatatypeBitFieldDescription
{
    public static ArrayBitFieldDescription Decode(H5DriverBase driver)
    {
        _ = driver.ReadBytes(3);

        return new ArrayBitFieldDescription(
            //
        );
    }
}

internal record class BitFieldBitFieldDescription(
    ByteOrder ByteOrder,
    bool PaddingTypeLow,
    bool PaddingTypeHigh
) : DatatypeBitFieldDescription, IByteOrderAware
{
    public static BitFieldBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new BitFieldBitFieldDescription(
            ByteOrder: (ByteOrder)(data[0] & 0x01),
            PaddingTypeLow: (data[0] >> 1) > 0,
            PaddingTypeHigh: (data[0] >> 2) > 0
        );
    }
}

internal record class CompoundBitFieldDescription(
    ushort MemberCount
) : DatatypeBitFieldDescription
{
    public static CompoundBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new CompoundBitFieldDescription(
            MemberCount: (ushort)(data[0] + (data[1] << 8))
        );
    }
}

internal record class EnumerationBitFieldDescription(
    ushort MemberCount
) : DatatypeBitFieldDescription
{
    public static EnumerationBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new EnumerationBitFieldDescription(
            MemberCount: (ushort)(data[0] + (data[1] << 8))
        );
    }
}

internal record class FixedPointBitFieldDescription(
    ByteOrder ByteOrder,
    bool PaddingTypeLow,
    bool PaddingTypeHigh,
    bool IsSigned
) : DatatypeBitFieldDescription, IByteOrderAware
{
    public static FixedPointBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new FixedPointBitFieldDescription(
            ByteOrder: (ByteOrder)(data[0] & 0x01),
            PaddingTypeLow: (data[0] >> 1) > 0,
            PaddingTypeHigh: (data[0] >> 2) > 0,
            IsSigned: (data[0] >> 3) > 0
        );
    }
}

internal record class FloatingPointBitFieldDescription(
    ByteOrder ByteOrder,
    bool PaddingTypeLow,
    bool PaddingTypeHigh,
    bool PaddingTypeInternal,
    MantissaNormalization MantissaNormalization,
    byte SignLocation
) : DatatypeBitFieldDescription, IByteOrderAware
{
    public static FloatingPointBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        // byte order
        ByteOrder byteOrder;

        var bit0 = (data[0] & (1 << 0)) > 0;
        var bit6 = (data[0] & (1 << 6)) > 0;

        if (!bit6)
        {
            byteOrder = bit0
                ? ByteOrder.BigEndian
                : ByteOrder.LittleEndian;
        }

        else
        {
            byteOrder = bit0 
                ? ByteOrder.VaxEndian
                : throw new NotSupportedException("In a floating-point bit field description bit 0 of the class bit field must be set when bit 6 is also set.");
        }

        return new FloatingPointBitFieldDescription(
            ByteOrder: byteOrder,
            PaddingTypeLow: (data[0] >> 1) > 0,
            PaddingTypeHigh: (data[0] >> 2) > 0,
            PaddingTypeInternal: (data[0] >> 3) > 0,
            MantissaNormalization: (MantissaNormalization)((data[0] >> 4) & 0x03),
            SignLocation: data[1]
        );
    }
}

internal record class OpaqueBitFieldDescription(
    byte AsciiTagByteLength
) : DatatypeBitFieldDescription
{
    public static OpaqueBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new OpaqueBitFieldDescription(
            AsciiTagByteLength: data[0]
        );
    }
}

internal record class ReferenceBitFieldDescription(
    InternalReferenceType Type
) : DatatypeBitFieldDescription
{
    public static ReferenceBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new ReferenceBitFieldDescription(
            Type: (InternalReferenceType)(data[0] & 0x0F)
        );
    }
}

internal record class StringBitFieldDescription(
    PaddingType PaddingType,
    CharacterSetEncoding Encoding
) : DatatypeBitFieldDescription
{
    public static StringBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new StringBitFieldDescription(
            PaddingType: (PaddingType)(data[0] & 0x0F),
            Encoding: (CharacterSetEncoding)((data[0] >> 4) & 0x01)
        );
    }
}

internal record class TimeBitFieldDescription(
    ByteOrder ByteOrder
) : DatatypeBitFieldDescription
{
    public static TimeBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new TimeBitFieldDescription(
            ByteOrder: (ByteOrder)(data[0] & 0x01)
        );
    }
}

internal record class VariableLengthBitFieldDescription(
    InternalVariableLengthType Type,
    PaddingType PaddingType,
    CharacterSetEncoding Encoding
) : DatatypeBitFieldDescription
{
    public static VariableLengthBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new VariableLengthBitFieldDescription(
            Type: (InternalVariableLengthType)(data[0] & 0x0F),
            PaddingType: (PaddingType)((data[0] & 0xF0) >> 4),
            Encoding: (CharacterSetEncoding)(data[1] & 0x0F)
        );
    }
}