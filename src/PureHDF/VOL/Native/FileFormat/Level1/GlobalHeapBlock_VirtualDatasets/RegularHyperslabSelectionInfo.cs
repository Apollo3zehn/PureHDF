namespace PureHDF.VOL.Native;

internal record class RegularHyperslabSelectionInfo(
    uint Rank,
    ulong[] Starts,
    ulong[] Strides,
    ulong[] Counts,
    ulong[] Blocks,
    ulong[] CompactDimensions
) : HyperslabSelectionInfo(Rank: Rank)
{
    public static async ValueTask<RegularHyperslabSelectionInfo> Decode(H5DriverBase driver, uint rank, byte encodeSize)
    {
        var starts = new ulong[rank];
        var strides = new ulong[rank];
        var counts = new ulong[rank];
        var blocks = new ulong[rank];

        var compactDimensions = new ulong[rank];

        for (int i = 0; i < rank; i++)
        {
            starts[i] = await H5S_SEL.ReadEncodedValue(driver, encodeSize).ConfigureAwait(false);
            strides[i] = await H5S_SEL.ReadEncodedValue(driver, encodeSize).ConfigureAwait(false);
            counts[i] = await H5S_SEL.ReadEncodedValue(driver, encodeSize).ConfigureAwait(false);
            blocks[i] = await H5S_SEL.ReadEncodedValue(driver, encodeSize).ConfigureAwait(false);

            compactDimensions[i] = blocks[i] * counts[i];
        }

        return new RegularHyperslabSelectionInfo(
            Rank: rank,
            Starts: starts,
            Strides: strides,
            Counts: counts,
            Blocks: blocks,
            CompactDimensions: compactDimensions
        );
    }
}