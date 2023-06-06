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

    public static VdsGlobalHeapBlock Decode(H5DriverBase localDriver, Superblock superblock)
    {
        // version
        var version = localDriver.ReadByte();

        // entry count
        var entryCount = superblock.ReadLength(localDriver);

        // vds dataset entries
        var vdsDatasetEntries = new VdsDatasetEntry[(int)entryCount];

        for (ulong i = 0; i < entryCount; i++)
        {
            vdsDatasetEntries[i] = VdsDatasetEntry.Decode(localDriver);
        }

        // checksum
        var _ = localDriver.ReadUInt32();

        return new VdsGlobalHeapBlock(
            VdsDatasetEntries: vdsDatasetEntries
        )
        {
            Version = version
        };
    }
}