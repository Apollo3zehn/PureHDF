namespace PureHDF.VOL.Native;

internal readonly record struct VdsGlobalHeapBlock(
    VdsDatasetEntry[] VdsDatasetEntries
)
{
    private readonly uint _version;

    public required uint Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(VdsGlobalHeapBlock)} are supported.");

            _version = value;
        }
    }

    public static async ValueTask<VdsGlobalHeapBlock> Decode(H5DriverBase localDriver, Superblock superblock)
    {
        // version
        var version = await localDriver.ReadByte().ConfigureAwait(false);

        // entry count
        var entryCount = await superblock.ReadLength(localDriver).ConfigureAwait(false);

        // vds dataset entries
        var vdsDatasetEntries = new VdsDatasetEntry[(int)entryCount];

        for (ulong i = 0; i < entryCount; i++)
        {
            vdsDatasetEntries[i] = await VdsDatasetEntry.Decode(localDriver).ConfigureAwait(false);
        }

        // checksum
        var _ = await localDriver.ReadUInt32().ConfigureAwait(false);

        return new VdsGlobalHeapBlock(
            VdsDatasetEntries: vdsDatasetEntries
        )
        {
            Version = version
        };
    }
}