namespace PureHDF.VOL.Native;

internal class EnumerationPropertyDescription : DatatypePropertyDescription
{
    #region Constructors

    internal EnumerationPropertyDescription(H5DriverBase driver, byte version, uint valueSize, ushort memberCount)
    {
        // base type
        BaseType = new DatatypeMessage(driver);

        // names
        Names = new List<string>(memberCount);

        for (int i = 0; i < memberCount; i++)
        {
            if (version <= 2)
                Names.Add(ReadUtils.ReadNullTerminatedString(driver, pad: true));
            else
                Names.Add(ReadUtils.ReadNullTerminatedString(driver, pad: false));
        }

        // values
        Values = new List<byte[]>(memberCount);

        for (int i = 0; i < memberCount; i++)
        {
            Values.Add(driver.ReadBytes((int)valueSize));
        }
    }

    #endregion

    #region Properties

    public DatatypeMessage BaseType { get; set; }
    public List<string> Names { get; set; }
    public List<byte[]> Values { get; set; }

    #endregion
}