namespace PureHDF.VOL.Native;

internal class SharedMessageTableMessage : Message
{
    #region Fields

    private byte _version;
    private readonly H5DriverBase _driver;

    #endregion

    #region Constructors

    public SharedMessageTableMessage(NativeContext context)
    {
        var (driver, superblock) = context;
        _driver = context.Driver;

        // version
        Version = driver.ReadByte();

        // shared object header message table address
        SharedObjectHeaderMessageTableAddress = superblock.ReadOffset(driver);

        // index count
        IndexCount = driver.ReadByte();
    }

    #endregion

    #region Properties

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(SharedMessageTableMessage)} are supported.");

            _version = value;
        }
    }

    public ulong SharedObjectHeaderMessageTableAddress { get; set; }
    public byte IndexCount { get; set; }

    public SharedObjectHeaderMessageTable SharedObjectHeaderMessageTable
    {
        get
        {
            _driver.Seek((long)SharedObjectHeaderMessageTableAddress, SeekOrigin.Begin);
            return SharedObjectHeaderMessageTable.Decode(_driver);
        }
    }

    #endregion
}