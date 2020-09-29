using System;

namespace HDF5.NET
{
    public class BitFieldBitFieldDescription : DatatypeBitFieldDescription, IByteOrderAware
    {
        #region Constructors

        public BitFieldBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public ByteOrder ByteOrder
        {
            get
            {
                return (ByteOrder)(this.Data[0] & 0x01);
            }
            set
            {
                switch (value)
                {
                    case ByteOrder.LittleEndian:
                        this.Data[0] &= 0xFE; break;

                    case ByteOrder.BigEndian:
                        this.Data[0] |= 0x01; break;

                    default:
                        throw new Exception($"On a bitfield bit field description the byte order value '{value}' is not supported.");
                }
            }
        }

        public bool PaddingTypeLow
        {
            get { return (this.Data[0] >> 1) > 0; }
            set { this.Data[0] |= (1 << 1); }
        }

        public bool PaddingTypeHigh
        {
            get { return (this.Data[0] >> 2) > 0; }
            set { this.Data[0] |= (1 << 2); }
        }

        #endregion
    }
}