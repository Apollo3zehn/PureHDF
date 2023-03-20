namespace PureHDF.VOL.Native;

internal class DatatypeMessage : Message
{
    #region Constructors

    public DatatypeMessage(H5DriverBase driver)
    {
        ClassVersion = driver.ReadByte();

        BitField = Class switch
        {
            DatatypeMessageClass.FixedPoint => new FixedPointBitFieldDescription(driver),
            DatatypeMessageClass.FloatingPoint => new FloatingPointBitFieldDescription(driver),
            DatatypeMessageClass.Time => new TimeBitFieldDescription(driver),
            DatatypeMessageClass.String => new StringBitFieldDescription(driver),
            DatatypeMessageClass.BitField => new BitFieldBitFieldDescription(driver),
            DatatypeMessageClass.Opaque => new OpaqueBitFieldDescription(driver),
            DatatypeMessageClass.Compound => new CompoundBitFieldDescription(driver),
            DatatypeMessageClass.Reference => new ReferenceBitFieldDescription(driver),
            DatatypeMessageClass.Enumerated => new EnumerationBitFieldDescription(driver),
            DatatypeMessageClass.VariableLength => new VariableLengthBitFieldDescription(driver),
            DatatypeMessageClass.Array => new ArrayBitFieldDescription(driver),
            _ => throw new NotSupportedException($"The data type message class '{Class}' is not supported.")
        };

        Size = driver.ReadUInt32();

        var memberCount = Class switch
        {
            DatatypeMessageClass.String => 0,
            DatatypeMessageClass.Reference => 0,
            DatatypeMessageClass.Compound => ((CompoundBitFieldDescription)BitField).MemberCount,
            _ => 1
        };

        Properties = new DatatypePropertyDescription[memberCount];

        for (int i = 0; i < memberCount; i++)
        {
            DatatypePropertyDescription properties = Class switch
            {
                DatatypeMessageClass.FixedPoint => new FixedPointPropertyDescription(driver),
                DatatypeMessageClass.FloatingPoint => new FloatingPointPropertyDescription(driver),
                DatatypeMessageClass.Time => new TimePropertyDescription(driver),
                DatatypeMessageClass.BitField => new BitFieldPropertyDescription(driver),
                DatatypeMessageClass.Opaque => new OpaquePropertyDescription(driver, GetOpaqueTagByteLength()),
                DatatypeMessageClass.Compound => new CompoundPropertyDescription(driver, Version, Size),
                DatatypeMessageClass.Enumerated => new EnumerationPropertyDescription(driver, Version, Size, GetEnumMemberCount()),
                DatatypeMessageClass.VariableLength => new VariableLengthPropertyDescription(driver),
                DatatypeMessageClass.Array => new ArrayPropertyDescription(driver, Version),
                _ => throw new NotSupportedException($"The class '{Class}' is not supported on data type messages of version {Version}.")
            };

            if (properties is not null)
                Properties[i] = properties;
        }
    }

    #endregion

    #region Properties

    public uint Size { get; set; }
    public DatatypeBitFieldDescription BitField { get; set; }
    public DatatypePropertyDescription[] Properties { get; set; }

    public byte Version
    {
        get
        {
            return (byte)(ClassVersion >> 4);
        }
        set
        {
            if (!(1 <= value && value <= 3))
                throw new Exception("The version number must be in the range of 1..3.");

            ClassVersion &= 0x0F;                  // clear bits 4-7
            ClassVersion |= (byte)(value << 4);    // set bits 4-7, depending on value
        }
    }

    public DatatypeMessageClass Class
    {
        get
        {
            return (DatatypeMessageClass)(ClassVersion & 0x0F);
        }
        set
        {
            if (!(0 <= (byte)value && (byte)value <= 10))
                throw new Exception("The version number must be in the range of 0..10.");

            ClassVersion &= 0xF0;          // clear bits 0-3
            ClassVersion |= (byte)value;   // set bits 0-3, depending on value
        }
    }

    private byte ClassVersion { get; set; }

    #endregion

    #region Method

    private byte GetOpaqueTagByteLength()
    {
        var opaqueDescription = BitField as OpaqueBitFieldDescription;

        if (opaqueDescription is not null)
            return opaqueDescription.AsciiTagByteLength;

        else
            throw new FormatException($"For opaque types, the bit field description must be an instance of type '{nameof(OpaqueBitFieldDescription)}'.");
    }

    private ushort GetEnumMemberCount()
    {
        var enumerationDescription = BitField as EnumerationBitFieldDescription;

        if (enumerationDescription is not null)
            return enumerationDescription.MemberCount;

        else
            throw new FormatException($"For enumeration types, the bit field description must be an instance of type '{nameof(EnumerationBitFieldDescription)}'.");
    }

    #endregion
}