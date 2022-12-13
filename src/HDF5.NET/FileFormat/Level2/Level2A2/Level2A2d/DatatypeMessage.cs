namespace HDF5.NET
{
    internal class DatatypeMessage : Message
    {
        #region Constructors

        public DatatypeMessage(H5BinaryReader reader) : base(reader)
        {
            ClassVersion = reader.ReadByte();

            BitField = Class switch
            {
                DatatypeMessageClass.FixedPoint     => new FixedPointBitFieldDescription(reader),
                DatatypeMessageClass.FloatingPoint  => new FloatingPointBitFieldDescription(reader),
                DatatypeMessageClass.Time           => new TimeBitFieldDescription(reader),
                DatatypeMessageClass.String         => new StringBitFieldDescription(reader),
                DatatypeMessageClass.BitField       => new BitFieldBitFieldDescription(reader),
                DatatypeMessageClass.Opaque         => new OpaqueBitFieldDescription(reader),
                DatatypeMessageClass.Compound       => new CompoundBitFieldDescription(reader),
                DatatypeMessageClass.Reference      => new ReferenceBitFieldDescription(reader),
                DatatypeMessageClass.Enumerated     => new EnumerationBitFieldDescription(reader),
                DatatypeMessageClass.VariableLength => new VariableLengthBitFieldDescription(reader),
                DatatypeMessageClass.Array          => new ArrayBitFieldDescription(reader),
                _ => throw new NotSupportedException($"The data type message class '{Class}' is not supported.")
            };

            Size = reader.ReadUInt32();

            var memberCount = 1;

            if (Class == DatatypeMessageClass.Compound)
                memberCount = ((CompoundBitFieldDescription)BitField).MemberCount;

            Properties = new List<DatatypePropertyDescription>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                DatatypePropertyDescription? properties = Class switch
                {
                    DatatypeMessageClass.FixedPoint => new FixedPointPropertyDescription(reader),
                    DatatypeMessageClass.FloatingPoint => new FloatingPointPropertyDescription(reader),
                    DatatypeMessageClass.Time => new TimePropertyDescription(reader),
                    DatatypeMessageClass.String => null,
                    DatatypeMessageClass.BitField => new BitFieldPropertyDescription(reader),
                    DatatypeMessageClass.Opaque => new OpaquePropertyDescription(reader, GetOpaqueTagByteLength()),
                    DatatypeMessageClass.Compound => new CompoundPropertyDescription(reader, Version, Size),
                    DatatypeMessageClass.Reference => null,
                    DatatypeMessageClass.Enumerated => new EnumerationPropertyDescription(reader, Version, Size, GetEnumMemberCount()),
                    DatatypeMessageClass.VariableLength => new VariableLengthPropertyDescription(reader),
                    DatatypeMessageClass.Array => new ArrayPropertyDescription(reader, Version),
                    _ => throw new NotSupportedException($"The class '{Class}' is not supported on data type messages of version {Version}.")
                };

                if (properties is not null)
                    Properties.Add(properties);
            }
        }

        #endregion

        #region Properties

        public DatatypeBitFieldDescription BitField { get; set; }
        public uint Size { get; set; }
        public List<DatatypePropertyDescription> Properties { get; set; }

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
}
