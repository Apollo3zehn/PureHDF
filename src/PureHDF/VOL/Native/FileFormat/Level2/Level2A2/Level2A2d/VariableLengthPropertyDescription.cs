namespace PureHDF.VOL.Native;

internal class VariableLengthPropertyDescription : DatatypePropertyDescription
{
    #region Constructors

    public VariableLengthPropertyDescription(H5DriverBase driver)
    {
        BaseType = new DatatypeMessage(driver);
    }

    #endregion

    #region Properties

    public DatatypeMessage BaseType { get; set; }

    #endregion
}