namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage(
    uint Size,
    DatatypeBitFieldDescription BitField,
    DatatypePropertyDescription[] Properties
) : Message
{
    private byte _version;

    private DatatypeMessageClass _class;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!(1 <= value && value <= 3))
                throw new Exception("The version number must be in the range of 1..3.");

            _version = value;
        }
    }

    public required DatatypeMessageClass Class
    {
        get
        {
            return _class;
        }
        init
        {
            if (!(0 <= (byte)value && (byte)value <= 10))
                throw new Exception("The class number must be in the range of 0..10.");

            _class = value;
        }
    }

    public static DatatypeMessage Decode(H5DriverBase driver)
    {
        var classVersion = driver.ReadByte();
        var version = (byte)(classVersion >> 4);
        var @class = (DatatypeMessageClass)(classVersion & 0x0F);

        DatatypeBitFieldDescription bitField = @class switch
        {
            DatatypeMessageClass.FixedPoint => FixedPointBitFieldDescription.Decode(driver),
            DatatypeMessageClass.FloatingPoint => FloatingPointBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Time => TimeBitFieldDescription.Decode(driver),
            DatatypeMessageClass.String => StringBitFieldDescription.Decode(driver),
            DatatypeMessageClass.BitField => BitFieldBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Opaque => OpaqueBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Compound => CompoundBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Reference => ReferenceBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Enumerated => EnumerationBitFieldDescription.Decode(driver),
            DatatypeMessageClass.VariableLength => VariableLengthBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Array => ArrayBitFieldDescription.Decode(driver),
            _ => throw new NotSupportedException($"The data type message class '{@class}' is not supported.")
        };

        var size = driver.ReadUInt32();

        var memberCount = @class switch
        {
            DatatypeMessageClass.String => 0,
            DatatypeMessageClass.Reference => 0,
            DatatypeMessageClass.Compound => ((CompoundBitFieldDescription)bitField).MemberCount,
            _ => 1
        };

        var properties = new DatatypePropertyDescription[memberCount];

        for (int i = 0; i < memberCount; i++)
        {
            DatatypePropertyDescription singleProperties = @class switch
            {
                DatatypeMessageClass.FixedPoint => FixedPointPropertyDescription.Decode(driver),
                DatatypeMessageClass.FloatingPoint => FloatingPointPropertyDescription.Decode(driver),
                DatatypeMessageClass.Time => TimePropertyDescription.Decode(driver),
                DatatypeMessageClass.BitField => BitFieldPropertyDescription.Decode(driver),
                DatatypeMessageClass.Opaque => OpaquePropertyDescription.Decode(driver, GetOpaqueTagByteLength(bitField)),
                DatatypeMessageClass.Compound => CompoundPropertyDescription.Decode(driver, version, size),
                DatatypeMessageClass.Enumerated => EnumerationPropertyDescription.Decode(driver, version, size, GetEnumMemberCount(bitField)),
                DatatypeMessageClass.VariableLength => VariableLengthPropertyDescription.Decode(driver),
                DatatypeMessageClass.Array => ArrayPropertyDescription.Decode(driver, version),
                _ => throw new NotSupportedException($"The data type message '{@class}' is not supported.")
            };

            if (singleProperties is not null)
                properties[i] = singleProperties;
        }

        return new DatatypeMessage(
            Size: size,
            BitField: bitField,
            Properties: properties
        )
        {
            Version = version,
            Class = @class
        };
    }

    private static byte GetOpaqueTagByteLength(DatatypeBitFieldDescription bitField)
    {
        var opaqueDescription = bitField as OpaqueBitFieldDescription;

        if (opaqueDescription is not null)
            return opaqueDescription.AsciiTagByteLength;

        else
            throw new FormatException($"For opaque types, the bit field description must be an instance of type '{nameof(OpaqueBitFieldDescription)}'.");
    }

    private static ushort GetEnumMemberCount(DatatypeBitFieldDescription bitField)
    {
        var enumerationDescription = bitField as EnumerationBitFieldDescription;

        if (enumerationDescription is not null)
            return enumerationDescription.MemberCount;

        else
            throw new FormatException($"For enumeration types, the bit field description must be an instance of type '{nameof(EnumerationBitFieldDescription)}'.");
    }
}