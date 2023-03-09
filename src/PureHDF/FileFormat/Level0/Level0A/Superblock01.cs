namespace PureHDF;

internal class Superblock01 : Superblock
{
    #region Fields

    readonly H5DriverBase _driver;

    #endregion

    #region Constructors

    public Superblock01(H5DriverBase driver, byte version)
    {
        _driver = driver;

        SuperBlockVersion = version;
        FreeSpaceStorageVersion = driver.ReadByte();
        RootGroupSymbolTableEntryVersion = driver.ReadByte();
        driver.ReadByte();

        SharedHeaderMessageFormatVersion = driver.ReadByte();
        OffsetsSize = driver.ReadByte();
        LengthsSize = driver.ReadByte();
        driver.ReadByte();

        GroupLeafNodeK = driver.ReadUInt16();
        GroupInternalNodeK = driver.ReadUInt16();

        FileConsistencyFlags = (FileConsistencyFlags)driver.ReadUInt32();

        if (SuperBlockVersion == 1)
        {
            IndexedStorageInternalNodeK = driver.ReadUInt16();
            driver.ReadUInt16();
        }

        BaseAddress = ReadOffset(driver);
        FreeSpaceInfoAddress = ReadOffset(driver);
        EndOfFileAddress = ReadOffset(driver);
        DriverInfoBlockAddress = ReadOffset(driver);

        var context = new H5Context(driver, this);
        RootGroupSymbolTableEntry = new SymbolTableEntry(context);
    }

    #endregion

    #region Properties

    public byte FreeSpaceStorageVersion { get; set; }
    public byte RootGroupSymbolTableEntryVersion { get; set; }
    public byte SharedHeaderMessageFormatVersion { get; set; }
    public ushort GroupLeafNodeK { get; set; }
    public ushort GroupInternalNodeK { get; set; }
    public ushort IndexedStorageInternalNodeK { get; set; }
    public ulong FreeSpaceInfoAddress { get; set; }
    public ulong DriverInfoBlockAddress { get; set; }
    public SymbolTableEntry RootGroupSymbolTableEntry { get; set; }

    public DriverInfoBlock? DriverInfoBlock
    {
        get
        {
            if (IsUndefinedAddress(DriverInfoBlockAddress))
                return null;

            else
                return new DriverInfoBlock(_driver);
        }
    }

    #endregion
}