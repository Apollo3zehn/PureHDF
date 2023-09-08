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
    public static Superblock23 Decode(H5DriverBase driver, byte version)
    {
        var offsetsSize = driver.ReadByte();
        var lengthsSize = driver.ReadByte();
        var fileConsistencyFlags = (FileConsistencyFlags)driver.ReadByte();
        var baseAddress = ReadUtils.ReadUlong(driver, offsetsSize);
        var extensionAddress = ReadUtils.ReadUlong(driver, offsetsSize);
        var endOfFileAddress = ReadUtils.ReadUlong(driver, offsetsSize);
        var rootGroupObjectHeaderAddress = ReadUtils.ReadUlong(driver, offsetsSize);
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

    // TODO: sample file: https://github.com/jamesmudd/jhdf/issues/462

    // public ObjectHeader Extension
    // {
    //     get
    //     {
    //         if (_extension is null)
    //         {
    //             Driver.Seek((long)ExtensionAddress, SeekOrigin.Begin);
    //             _extension = ObjectHeader.Construct(new NativeReadContext(Driver, this));
    //         }
            
    //         return _extension;
    //     }
    // }
}