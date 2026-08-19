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

    public static async ValueTask<Superblock01> Decode(H5DriverBase driver, byte version)
    {
        var superBlockVersion = version;
        var freeSpaceStorageVersion = await driver.ReadByte().ConfigureAwait(false);
        var rootGroupSymbolTableEntryVersion = await driver.ReadByte().ConfigureAwait(false);
        await driver.ReadByte().ConfigureAwait(false);

        var sharedHeaderMessageFormatVersion = await driver.ReadByte().ConfigureAwait(false);
        var offsetsSize = await driver.ReadByte().ConfigureAwait(false);
        var lengthsSize = await driver.ReadByte().ConfigureAwait(false);
        await driver.ReadByte().ConfigureAwait(false);

        var groupLeafNodeK = await driver.ReadUInt16().ConfigureAwait(false);
        var groupInternalNodeK = await driver.ReadUInt16().ConfigureAwait(false);

        var fileConsistencyFlags = (FileConsistencyFlags)await driver.ReadUInt32().ConfigureAwait(false);
        var indexedStorageInternalNodeK = default(ushort);

        if (superBlockVersion == 1)
        {
            indexedStorageInternalNodeK = await driver.ReadUInt16().ConfigureAwait(false);
            await driver.ReadUInt16().ConfigureAwait(false);
        }

        var baseAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);
        var freeSpaceInfoAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);
        var endOfFileAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);
        var driverInfoBlockAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);

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

        var context = new NativeReadContext(driver, superblock) { ReadOptions = new() };
        var rootGroupSymbolTableEntry = await SymbolTableEntry.Decode(context).ConfigureAwait(false);

        superblock.RootGroupSymbolTableEntry = rootGroupSymbolTableEntry;

        return superblock;
    }

    public async ValueTask<DriverInfoBlock?> GetDriverInfoBlock()
    {
        if (IsUndefinedAddress(DriverInfoBlockAddress))
        {
            return default;
        }

        else
        {
            _driverInfoBlock ??= await Native.DriverInfoBlock.Decode(Driver).ConfigureAwait(false);
            return _driverInfoBlock;
        }
    }
}