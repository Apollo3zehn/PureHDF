namespace PureHDF.VOL.Native;

internal class Superblock23 : Superblock
{
    private ObjectHeader? _extension;

    private readonly H5DriverBase _driver;

    #region Constructors

    public Superblock23(H5DriverBase driver, byte version)
    {
        _driver = driver;

        SuperBlockVersion = version;
        OffsetsSize = driver.ReadByte();
        LengthsSize = driver.ReadByte();
        FileConsistencyFlags = (FileConsistencyFlags)driver.ReadByte();
        BaseAddress = ReadOffset(driver);
        ExtensionAddress = ReadOffset(driver);
        EndOfFileAddress = ReadOffset(driver);
        RootGroupObjectHeaderAddress = ReadOffset(driver);
        Checksum = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public ObjectHeader Extension
    {
        // sample file: https://github.com/jamesmudd/jhdf/issues/462
        get
        {
            if (_extension is null)
            {
                _driver.Seek((long)ExtensionAddress, SeekOrigin.Begin);
                _extension = ObjectHeader.Construct(new NativeContext(_driver, this));
            }
            
            return _extension;
        }
    }

    public ulong ExtensionAddress { get; set; }
    public ulong RootGroupObjectHeaderAddress { get; set; }
    public uint Checksum { get; set; }

    #endregion
}