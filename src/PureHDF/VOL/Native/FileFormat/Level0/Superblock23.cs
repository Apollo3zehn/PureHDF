namespace PureHDF.VOL.Native;

internal partial record class Superblock23(
    H5DriverBase Driver,
    byte Version,
    FileConsistencyFlags FileConsistencyFlags,
    ulong BaseAddress,
    ulong ExtensionAddress,
    ulong EndOfFileAddress,
    ulong RootGroupObjectHeaderAddress
): Superblock(
    Version, 
    FileConsistencyFlags, 
    BaseAddress, 
    EndOfFileAddress
)
{
    private ObjectHeader? _extension;

    public static Superblock23 Decode(H5DriverBase driver, byte version)
    {
        var offsetsSize = driver.ReadByte();
        var lengthsSize = driver.ReadByte();
        var fileConsistencyFlags = (FileConsistencyFlags)driver.ReadByte();
        var baseAddress = Utils.ReadUlong(driver, offsetsSize);
        var extensionAddress = Utils.ReadUlong(driver, offsetsSize);
        var endOfFileAddress = Utils.ReadUlong(driver, offsetsSize);
        var rootGroupObjectHeaderAddress = Utils.ReadUlong(driver, offsetsSize);
        var _ = driver.ReadUInt32();

        return new Superblock23(
            driver,
            version,
            fileConsistencyFlags,
            baseAddress,
            extensionAddress,
            endOfFileAddress,
            rootGroupObjectHeaderAddress
        )
        {
            OffsetsSize = offsetsSize,
            LengthsSize = lengthsSize
        };
    }

    public ObjectHeader Extension
    {
        // sample file: https://github.com/jamesmudd/jhdf/issues/462
        get
        {
            if (_extension is null)
            {
                Driver.Seek((long)ExtensionAddress, SeekOrigin.Begin);
                _extension = ObjectHeader.Construct(new NativeContext(Driver, this));
            }
            
            return _extension;
        }
    }
}