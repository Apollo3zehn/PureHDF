namespace PureHDF.VOL.Native;

internal class OpaqueBitFieldDescription : DatatypeBitFieldDescription
{
    #region Constructors

    public OpaqueBitFieldDescription(H5DriverBase driver) : base(driver)
    {
        //
    }

    #endregion

    #region Properties

    public byte AsciiTagByteLength
    {
        get { return Data[0]; }
        set { Data[0] = value; }
    }

    #endregion
}