namespace PureHDF.VOL.Native;

internal readonly record struct VdsDatasetEntry(
    string SourceFileName,
    string SourceDataset,
    DataspaceSelection SourceSelection,
    DataspaceSelection VirtualSelection
)
{
    public static async ValueTask<VdsDatasetEntry> Decode(H5DriverBase driver)
    {
        return new VdsDatasetEntry(
            SourceFileName: await ReadUtils.ReadNullTerminatedString(driver, pad: false).ConfigureAwait(false),
            SourceDataset: await ReadUtils.ReadNullTerminatedString(driver, pad: false).ConfigureAwait(false),
            SourceSelection: await DataspaceSelection.Decode(driver).ConfigureAwait(false),
            VirtualSelection: await DataspaceSelection.Decode(driver).ConfigureAwait(false)
        );
    }
}