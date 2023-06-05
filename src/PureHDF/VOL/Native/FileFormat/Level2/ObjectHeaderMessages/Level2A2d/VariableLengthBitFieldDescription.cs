namespace PureHDF.VOL.Native;

internal class VariableLengthBitFieldDescription : DatatypeBitFieldDescription
{
    #region Constructors

    public VariableLengthBitFieldDescription(H5DriverBase driver) : base(driver)
    {
        //
    }

    #endregion

    #region Properties

    public InternalVariableLengthType Type
    {
        get
        {
            return (InternalVariableLengthType)(Data[0] & 0x0F);
        }
        set
        {
            Data[0] &= 0xF0;           // clear bits 0-3
            Data[0] |= (byte)value;    // set bits 0-3, depending on the value
        }
    }

    public PaddingType PaddingType
    {
        get
        {
            return (PaddingType)((Data[0] & 0xF0) >> 4);
        }
        set
        {
            Data[0] &= 0x0F;                       // clear bits 4-7
            Data[0] |= (byte)((byte)value << 4);   // set bits 4-7, depending on the value
        }
    }

    public CharacterSetEncoding Encoding
    {
        get
        {
            return (CharacterSetEncoding)(Data[1] & 0x0F);
        }
        set
        {
            Data[1] &= 0xF0;           // clear bits 0-3
            Data[1] |= (byte)value;    // set bits 0-3, depending on the value
        }
    }

    #endregion
}