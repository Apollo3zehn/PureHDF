using System.IO;

namespace HDF5.NET
{
    public class VariableLengthBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public VariableLengthBitFieldDescription(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public VariableLengthType Type
        {
            get
            {
                return (VariableLengthType)(this.Data[0] & 0x0F);
            }
            set
            {
                this.Data[0] &= 0xF0;           // clear bits 0-3
                this.Data[0] |= (byte)value;    // set bits 0-3, depending on the value
            }
        }

        public PaddingType PaddingType
        {
            get
            {
                return (PaddingType)((this.Data[0] & 0xF0) >> 4);
            }
            set
            {
                this.Data[0] &= 0x0F;                       // clear bits 4-7
                this.Data[0] |= (byte)((byte)value << 4);   // set bits 4-7, depending on the value
            }
        }

        public CharacterSetEncoding CharacterSet
        {
            get
            {
                return (CharacterSetEncoding)(this.Data[1] & 0x0F);
            }
            set
            {
                this.Data[1] &= 0xF0;           // clear bits 0-3
                this.Data[1] |= (byte)value;    // set bits 0-3, depending on the value
            }
        }

        #endregion
    }
}
