﻿namespace PureHDF
{
    internal class TimeBitFieldDescription : DatatypeBitFieldDescription, IByteOrderAware
    {
        #region Constructors

        public TimeBitFieldDescription(H5DriverBase driver) : base(driver)
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
                        throw new Exception($"On a time bit field description the byte order value '{value}' is not supported.");
                }
            }
        }

        #endregion
    }
}
