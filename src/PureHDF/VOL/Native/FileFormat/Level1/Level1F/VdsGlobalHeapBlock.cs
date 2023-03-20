namespace PureHDF.VOL.Native;

internal class VdsGlobalHeapBlock
{
    #region Fields

    private uint _version;

    #endregion

    #region Constructors

    public VdsGlobalHeapBlock(H5DriverBase localDriver, Superblock superblock)
    {
        // version
        Version = localDriver.ReadByte();

        // entry count
        var entryCount = superblock.ReadLength(localDriver);

        // vds dataset entries
        VdsDatasetEntries = new VdsDatasetEntry[(int)entryCount];

        for (ulong i = 0; i < entryCount; i++)
        {
            VdsDatasetEntries[i] = new VdsDatasetEntry(localDriver);
        }

        // checksum
        Checksum = localDriver.ReadUInt32();
    }

    #endregion

    #region Properties

    public uint Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(VdsGlobalHeapBlock)} are supported.");

            _version = value;
        }
    }

    public VdsDatasetEntry[] VdsDatasetEntries { get; set; }
    public uint Checksum { get; set; }

    #endregion
}