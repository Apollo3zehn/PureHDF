namespace PureHDF.VOL.Native;

internal class FamilyDriverInfo : DriverInfo
{
    #region Constructors

    public FamilyDriverInfo(H5DriverBase driver)
    {
        MemberFileSize = driver.ReadUInt64();
    }

    #endregion

    #region Properties

    public ulong MemberFileSize { get; set; }

    #endregion
}