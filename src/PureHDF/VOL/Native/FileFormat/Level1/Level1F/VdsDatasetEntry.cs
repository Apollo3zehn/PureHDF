namespace PureHDF.VOL.Native;

internal class VdsDatasetEntry
{
    #region Constructors

    public VdsDatasetEntry(H5DriverBase driver)
    {
        // source file name
        SourceFileName = ReadUtils.ReadNullTerminatedString(driver, pad: false);

        // source dataset
        SourceDataset = ReadUtils.ReadNullTerminatedString(driver, pad: false);

        // source selection
        SourceSelection = new DataspaceSelection(driver);

        // virtual selection
        VirtualSelection = new DataspaceSelection(driver);
    }

    #endregion

    #region Properties

    public string SourceFileName { get; set; }
    public string SourceDataset { get; set; }
    public DataspaceSelection SourceSelection { get; set; }
    public DataspaceSelection VirtualSelection { get; set; }

    #endregion
}