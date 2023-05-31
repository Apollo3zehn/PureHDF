namespace PureHDF.VOL.Native;

internal record class Superblock01(
    H5DriverBase Driver,
    byte SuperBlockVersion,
    byte FreeSpaceStorageVersion,
    byte RootGroupSymbolTableEntryVersion,
    byte SharedHeaderMessageFormatVersion,
    ushort GroupLeafNodeK,
    ushort GroupInternalNodeK,
    FileConsistencyFlags FileConsistencyFlags,
    ushort IndexedStorageInternalNodeK,
    ulong BaseAddress,
    ulong FreeSpaceInfoAddress,
    ulong EndOfFileAddress,
    ulong DriverInfoBlockAddress
) : Superblock(
    SuperBlockVersion, 
    FileConsistencyFlags, 
    BaseAddress, 
    EndOfFileAddress
)
{
    private DriverInfoBlock? _driverInfoBlock;

    public SymbolTableEntry RootGroupSymbolTableEntry { get; private set; } = default!;

    public static Superblock01 Decode(H5DriverBase driver, byte version)
    {
        var superBlockVersion = version;
        var freeSpaceStorageVersion = driver.ReadByte();
        var rootGroupSymbolTableEntryVersion = driver.ReadByte();
        driver.ReadByte();

        var sharedHeaderMessageFormatVersion = driver.ReadByte();
        var offsetsSize = driver.ReadByte();
        var lengthsSize = driver.ReadByte();
        driver.ReadByte();

        var groupLeafNodeK = driver.ReadUInt16();
        var groupInternalNodeK = driver.ReadUInt16();

        var fileConsistencyFlags = (FileConsistencyFlags)driver.ReadUInt32();
        var indexedStorageInternalNodeK = default(ushort);

        if (superBlockVersion == 1)
        {
            indexedStorageInternalNodeK = driver.ReadUInt16();
            driver.ReadUInt16();
        }

        var baseAddress = Utils.ReadUlong(driver, offsetsSize);
        var freeSpaceInfoAddress = Utils.ReadUlong(driver, offsetsSize);
        var endOfFileAddress = Utils.ReadUlong(driver, offsetsSize);
        var driverInfoBlockAddress = Utils.ReadUlong(driver, offsetsSize);

        var superblock = new Superblock01(
            driver,
            superBlockVersion,
            freeSpaceStorageVersion,
            rootGroupSymbolTableEntryVersion,
            sharedHeaderMessageFormatVersion,
            groupLeafNodeK,
            groupInternalNodeK,
            fileConsistencyFlags,
            indexedStorageInternalNodeK,
            baseAddress,
            freeSpaceInfoAddress,
            endOfFileAddress,
            driverInfoBlockAddress
        )
        {
            OffsetsSize = offsetsSize,
            LengthsSize = lengthsSize
        };

        var context = new NativeContext(driver, superblock);
        var rootGroupSymbolTableEntry = new SymbolTableEntry(context);

        superblock.RootGroupSymbolTableEntry = rootGroupSymbolTableEntry;

        return superblock;
    }

    public DriverInfoBlock? DriverInfoBlock
    {
        get
        {
            if (IsUndefinedAddress(DriverInfoBlockAddress))
            {
                return default;
            }

            else
            {
                _driverInfoBlock ??= Native.DriverInfoBlock.Decode(Driver);
                return _driverInfoBlock;
            }
        }
    }
}