namespace HDF5.NET
{
    internal class FloatingPointBitFieldDescription : DatatypeBitFieldDescription, IByteOrderAware
    {
        #region Constructors

        public FloatingPointBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public ByteOrder ByteOrder
        {
            get
            {
                var bit0 = (Data[0] & (1 << 0)) > 0;
                var bit6 = (Data[0] & (1 << 6)) > 0;

                if (!bit6)
                {
                    if (!bit0)
                        return ByteOrder.LittleEndian;
                    else
                        return ByteOrder.BigEndian;
                }
                else
                {
                    if (!bit0)
                        throw new NotSupportedException("In a floating-point bit field description bit 0 of the class bit field must be set when bit 6 is also set.");
                    else
                        return ByteOrder.VaxEndian;
                }
            }
            set
            {
                switch (value)
                {
                    case ByteOrder.LittleEndian:
                        Data[0] &= 0xBE; // clear bit 0 and 6
                        break;

                    case ByteOrder.BigEndian:
                        Data[0] |= 0x01; // set bit 0
                        Data[0] &= 0xBF; // clear bit 6
                        break;

                    case ByteOrder.VaxEndian:
                        Data[0] |= 0x41; // set bit 0 and 6
                        break;

                    default:
                        throw new Exception($"On a floating-point bit field description the byte order value '{value}' is not supported.");
                }
            }
        }

        public bool PaddingTypeLow
        {
            get { return (Data[0] >> 1) > 0; }
            set { Data[0] |= (1 << 1); }
        }

        public bool PaddingTypeHigh
        {
            get { return (Data[0] >> 2) > 0; }
            set { Data[0] |= (1 << 2); }
        }

        public bool PaddingTypeInternal
        {
            get { return (Data[0] >> 3) > 0; }
            set { Data[0] |= (1 << 3); }
        }

        public MantissaNormalization MantissaNormalization
        {
            get
            {
                return (MantissaNormalization)((Data[0] >> 4) & 0x03);
            }
            set
            {
                Data[0] &= 0xC0;                       // clear bit 4 and 5
                Data[0] |= (byte)((byte)value << 4);   // set   bit 4 or 5, depending on the value 
            }
        }

        public byte SignLocation 
        {
            get { return Data[1]; }
            set { Data[1] = value; }
        }

        #endregion
    }
}
