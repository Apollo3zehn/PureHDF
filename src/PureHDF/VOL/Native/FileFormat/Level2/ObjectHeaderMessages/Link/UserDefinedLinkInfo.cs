namespace PureHDF.VOL.Native;

internal class UserDefinedLinkInfo : LinkInfo
{
    #region Constructors

    public UserDefinedLinkInfo(H5DriverBase driver)
    {
        // data length
        DataLength = driver.ReadUInt16();

        // data
        Data = driver.ReadBytes(DataLength);
    }

    #endregion

    #region Properties

    public ushort DataLength { get; set; }

    public byte[] Data { get; set; }

    #endregion
}