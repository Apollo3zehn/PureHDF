namespace PureHDF.VOL.Native;

internal class StringBitFieldDescription : DatatypeBitFieldDescription
{
    #region Constructors

    public StringBitFieldDescription(H5DriverBase driver) : base(driver)
    {
        //
    }

    #endregion

    #region Properties

    public PaddingType PaddingType
    {
        get
        {
            return (PaddingType)(Data[0] & 0x0F);
        }
        set
        {
            Data[0] &= 0xF0;           // clear bits 0-3
            Data[0] |= (byte)value;    // set bits 0-3, depending on the value
        }
    }

    public CharacterSetEncoding Encoding
    {
        get
        {
            return (CharacterSetEncoding)((Data[0] >> 4) & 0x01);
        }
        set
        {
            switch (value)
            {
                case CharacterSetEncoding.ASCII:
                    Data[0] &= 0xEF; break; // clear bit 4

                case CharacterSetEncoding.UTF8:
                    Data[0] |= 0x10; break; // set bit 4

                default:
                    throw new Exception($"On a string bit field description the value '{value}' is not supported.");
            }
        }
    }

    #endregion
}