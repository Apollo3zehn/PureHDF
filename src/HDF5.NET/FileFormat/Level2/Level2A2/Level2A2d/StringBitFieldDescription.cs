using System;

namespace HDF5.NET
{
    public class StringBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public StringBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public PaddingType PaddingType 
        {
            get 
            {
                return (PaddingType)(this.Data[0] & 0x0F);
            }
            set 
            {
                this.Data[0] &= 0xF0;           // clear bits 0-3
                this.Data[0] |= (byte)value;    // set bits 0-3, depending on the value
            }
        }

        public CharacterSetEncoding Encoding
        {
            get 
            {
                return (CharacterSetEncoding)((this.Data[0] >> 4) & 0x01); 
            }
            set 
            {
                switch (value)
                {
                    case CharacterSetEncoding.ASCII:
                        this.Data[0] &= 0xEF; break; // clear bit 4

                    case CharacterSetEncoding.UTF8:
                        this.Data[0] |= 0x10; break; // set bit 4

                    default:
                        throw new Exception($"On a string bit field description the value '{value}' is not supported.");
                }
            }
        }

        #endregion
    }
}
