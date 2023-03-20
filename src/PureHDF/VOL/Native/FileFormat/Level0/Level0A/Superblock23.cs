namespace PureHDF.VOL.Native;

internal class Superblock23 : Superblock
{
    #region Constructors

    public Superblock23(H5DriverBase driver, byte version)
    {
        SuperBlockVersion = version;
        OffsetsSize = driver.ReadByte();
        LengthsSize = driver.ReadByte();
        FileConsistencyFlags = (FileConsistencyFlags)driver.ReadByte();
        BaseAddress = ReadOffset(driver);
        SuperblockExtensionAddress = ReadOffset(driver);
        EndOfFileAddress = ReadOffset(driver);
        RootGroupObjectHeaderAddress = ReadOffset(driver);
        SuperblockChecksum = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public ulong SuperblockExtensionAddress { get; set; }
    public ulong RootGroupObjectHeaderAddress { get; set; }
    public uint SuperblockChecksum { get; set; }

    #endregion
}