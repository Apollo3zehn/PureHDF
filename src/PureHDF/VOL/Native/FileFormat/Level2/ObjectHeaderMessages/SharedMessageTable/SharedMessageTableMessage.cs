namespace PureHDF.VOL.Native;

internal record class SharedMessageTableMessage(
    H5DriverBase Driver,
    ulong SharedObjectHeaderMessageTableAddress,
    byte IndexCount
) : Message
{
    private byte _version;
    private SharedObjectHeaderMessageTable _sharedObjectHeaderMessageTable;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(SharedMessageTableMessage)} are supported.");

            _version = value;
        }
    }

    // A method rather than a property: C# has no async property getters. No callers exist in the
    // repo today.
    public async ValueTask<SharedObjectHeaderMessageTable> GetSharedObjectHeaderMessageTable()
    {
        if (_sharedObjectHeaderMessageTable.Equals(default))
        {
            Driver.SeekRelativeToBaseAddress((long)SharedObjectHeaderMessageTableAddress);
            _sharedObjectHeaderMessageTable = await SharedObjectHeaderMessageTable.Decode(Driver).ConfigureAwait(false);
        }

        return _sharedObjectHeaderMessageTable;
    }

    public static async ValueTask<SharedMessageTableMessage> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // shared object header message table address
        var sharedObjectHeaderMessageTableAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // index count
        var indexCount = await driver.ReadByte().ConfigureAwait(false);

        return new SharedMessageTableMessage(
            Driver: driver,
            SharedObjectHeaderMessageTableAddress: sharedObjectHeaderMessageTableAddress,
            IndexCount: indexCount
        )
        {
            Version = version
        };
    }
}