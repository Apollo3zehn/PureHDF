namespace PureHDF.VOL.Native;

internal partial record class Superblock23(
    H5DriverBase Driver,
    byte Version,
    FileConsistencyFlags FileConsistencyFlags,
    ulong BaseAddress,
    ulong ExtensionAddress,
    ulong EndOfFileAddress,
    ulong RootGroupObjectHeaderAddress
) : Superblock(
    Version,
    FileConsistencyFlags,
    BaseAddress,
    EndOfFileAddress
)
{
    public static async ValueTask<Superblock23> Decode(H5DriverBase driver, byte version)
    {
        var offsetsSize = await driver.ReadByte().ConfigureAwait(false);
        var lengthsSize = await driver.ReadByte().ConfigureAwait(false);
        var fileConsistencyFlags = (FileConsistencyFlags)(await driver.ReadByte().ConfigureAwait(false));
        var baseAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);
        var extensionAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);
        var endOfFileAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);
        var rootGroupObjectHeaderAddress = await ReadUtils.ReadUlong(driver, offsetsSize).ConfigureAwait(false);
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

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
    //             Driver.SeekRelativeToBaseAddressSeek((long)ExtensionAddress);
    //             _extension = ObjectHeader.Construct(new NativeReadContext(Driver, this));
    //         }

    //         return _extension;
    //     }
    // }
}