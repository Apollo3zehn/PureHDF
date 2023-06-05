namespace PureHDF.VOL.Native;

internal class FloatingPointPropertyDescription : DatatypePropertyDescription
{
    #region Constructors

    public FloatingPointPropertyDescription(H5DriverBase driver)
    {
        BitOffset = driver.ReadUInt16();
        BitPrecision = driver.ReadUInt16();
        ExponentLocation = driver.ReadByte();
        ExponentSize = driver.ReadByte();
        MantissaLocation = driver.ReadByte();
        MantissaSize = driver.ReadByte();
        ExponentBias = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public ushort BitOffset { get; set; }
    public ushort BitPrecision { get; set; }
    public byte ExponentLocation { get; set; }
    public byte ExponentSize { get; set; }
    public byte MantissaLocation { get; set; }
    public byte MantissaSize { get; set; }
    public uint ExponentBias { get; set; }

    #endregion
}