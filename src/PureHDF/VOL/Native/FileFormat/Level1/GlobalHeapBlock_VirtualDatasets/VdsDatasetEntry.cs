namespace PureHDF.VOL.Native;

internal readonly record struct VdsDatasetEntry(
    string SourceFileName,
    string SourceDataset,
    DataspaceSelection SourceSelection,
    DataspaceSelection VirtualSelection
)
{
    public static VdsDatasetEntry Decode(H5DriverBase driver)
    {
        return new VdsDatasetEntry(
            SourceFileName: ReadUtils.ReadNullTerminatedString(driver, pad: false),
            SourceDataset: ReadUtils.ReadNullTerminatedString(driver, pad: false),
            SourceSelection: DataspaceSelection.Decode(driver),
            VirtualSelection: DataspaceSelection.Decode(driver)
        );
    }
}