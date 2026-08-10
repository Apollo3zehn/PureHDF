namespace PureHDF.VOL.Native;

internal abstract record class HyperslabSelectionInfo(
    uint Rank
)
{
    public static async ValueTask<HyperslabSelectionInfo> Create(H5DriverBase driver, uint version)
    {
        uint rank;

        switch (version)
        {
            case 1:
                // reserved
                _ = await driver.ReadBytes(4).ConfigureAwait(false);

                // length
                _ = await driver.ReadUInt32().ConfigureAwait(false);

                // rank
                rank = await driver.ReadUInt32().ConfigureAwait(false);

                return await IrregularHyperslabSelectionInfo.Decode(driver, rank, encodeSize: 4).ConfigureAwait(false);

            case 2:
                // flags
                _ = await driver.ReadByte().ConfigureAwait(false);

                // length
                _ = await driver.ReadUInt32().ConfigureAwait(false);

                // rank
                rank = await driver.ReadUInt32().ConfigureAwait(false);

                return await RegularHyperslabSelectionInfo.Decode(driver, rank, encodeSize: 8).ConfigureAwait(false);

            case 3:
                // flags
                var flags = await driver.ReadByte().ConfigureAwait(false);

                // encode size
                var encodeSize = await driver.ReadByte().ConfigureAwait(false);

                // rank
                rank = await driver.ReadUInt32().ConfigureAwait(false);

                if ((flags & 0x01) == 1)
                    return await RegularHyperslabSelectionInfo.Decode(driver, rank, encodeSize).ConfigureAwait(false);
                else
                    return await IrregularHyperslabSelectionInfo.Decode(driver, rank, encodeSize).ConfigureAwait(false);

            default:
                throw new NotSupportedException($"Only {nameof(H5S_SEL_HYPER)} of version 1, 2 or 3 are supported.");
        }
    }
}