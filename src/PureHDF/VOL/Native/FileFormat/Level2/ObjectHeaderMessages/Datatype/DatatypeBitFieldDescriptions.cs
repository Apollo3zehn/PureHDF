namespace PureHDF.VOL.Native;

internal abstract record class DatatypeBitFieldDescription
{
    public abstract void Encode(H5DriverBase driver);
};

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

    public override void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
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

    public override void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
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

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        data[0] = (byte)(MemberCount & 0xFF);
        data[1] = (byte)((MemberCount >> 8) & 0xFF);

        driver.Write(data);
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

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        data[0] = (byte)(MemberCount & 0xFF);
        data[1] = (byte)((MemberCount >> 8) & 0xFF);

        driver.Write(data);
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

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        data[0] = (byte)(
            (byte)ByteOrder & 0x01 |
            (PaddingTypeLow ? 1 : 0) << 1 |
            (PaddingTypeHigh ? 1 : 0) << 2 |
            (IsSigned ? 1 : 0) << 3
        );

        driver.Write(data);
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

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        if (ByteOrder == ByteOrder.LittleEndian)
            data[0] = 0 << 0;

        else if (ByteOrder == ByteOrder.BigEndian)
            data[0] = 1 << 0;

        else if (ByteOrder == ByteOrder.VaxEndian)
            data[0] = (1 << 0) | (1 << 6);

        data[0] = (byte)(
            data[0] |
            (PaddingTypeLow ? 1 : 0) << 1 |
            (PaddingTypeHigh ? 1 : 0) << 2 |
            (PaddingTypeInternal ? 1 : 0) << 3 |
            ((byte)MantissaNormalization & 0x03) << 4
        );

        data[1] = SignLocation;

        driver.Write(data);
    }
}

internal record class OpaqueBitFieldDescription(
    byte TagByteLength
) : DatatypeBitFieldDescription
{
    public static OpaqueBitFieldDescription Decode(H5DriverBase driver)
    {
        var data = driver.ReadBytes(3);

        return new OpaqueBitFieldDescription(
            TagByteLength: data[0]
        );
    }

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        data[0] = TagByteLength;

        driver.Write(data);
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

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        data[0] = (byte)((byte)Type & 0x07);

        driver.Write(data);
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

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        data[0] = (byte)((byte)PaddingType & 0x0F);
        data[0] |= (byte)(((byte)Encoding & 0x0F) << 4);

        driver.Write(data);
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

    public override void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
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

    public override void Encode(H5DriverBase driver)
    {
        Span<byte> data = stackalloc byte[3];

        data[0] = (byte)((byte)Type & 0x0F);
        data[0] |= (byte)(((byte)PaddingType & 0x0F) << 4);

        var encoding = Type == InternalVariableLengthType.String
            ? Encoding
            : 0;

        data[1] = (byte)((byte)encoding & 0x0F);

        driver.Write(data);
    }
}