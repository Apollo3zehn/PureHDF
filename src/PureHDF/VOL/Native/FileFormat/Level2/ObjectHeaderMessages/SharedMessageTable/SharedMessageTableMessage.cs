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

    public SharedObjectHeaderMessageTable SharedObjectHeaderMessageTable
    {
        get
        {
            if (_sharedObjectHeaderMessageTable.Equals(default))
            {
                Driver.Seek((long)SharedObjectHeaderMessageTableAddress, SeekOrigin.Begin);
                _sharedObjectHeaderMessageTable = SharedObjectHeaderMessageTable.Decode(Driver);
            }

            return _sharedObjectHeaderMessageTable;
        }
    }

    public static SharedMessageTableMessage Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // version
        var version = driver.ReadByte();

        // shared object header message table address
        var sharedObjectHeaderMessageTableAddress = superblock.ReadOffset(driver);

        // index count
        var indexCount = driver.ReadByte();

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