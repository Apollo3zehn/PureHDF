using System;

namespace HDF5.NET
{
    public class TimeBitFieldDescription : DatatypeBitFieldDescription, IByteOrderAware
    {
        #region Constructors

        public TimeBitFieldDescription(H5BinaryReader reader) : base(reader)
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
                        throw new Exception($"On a time bit field description the byte order value '{value}' is not supported.");
                }
            }
        }

        #endregion
    }
}
