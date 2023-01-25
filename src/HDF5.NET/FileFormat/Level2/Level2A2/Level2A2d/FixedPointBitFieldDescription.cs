namespace HDF5.NET
{
    internal class FixedPointBitFieldDescription : DatatypeBitFieldDescription, IByteOrderAware
    {
        #region Constructors

        public FixedPointBitFieldDescription(H5BaseReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public ByteOrder ByteOrder
        {
            get
            {
                return (ByteOrder)(Data[0] & 0x01);
            }
            set
            {
                switch (value)
                {
                    case ByteOrder.LittleEndian:
                        Data[0] &= 0xFE; break;

                    case ByteOrder.BigEndian:
                        Data[0] |= 0x01; break;

                    default:
                        throw new Exception($"On a fixed-point bit field description the byte order value '{value}' is not supported.");
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

        public bool IsSigned
        {
            get { return (Data[0] >> 3) > 0; }
            set { Data[0] |= (1 << 3); }
        }

        #endregion
    }
}
