namespace PureHDF.VOL.Native;

internal class TimePropertyDescription : DatatypePropertyDescription
{
    #region Constructors

    public TimePropertyDescription(H5DriverBase driver)
    {
        BitPrecision = driver.ReadUInt16();
    }

    #endregion

    #region Properties

    public ushort BitPrecision { get; set; }

    #endregion
}