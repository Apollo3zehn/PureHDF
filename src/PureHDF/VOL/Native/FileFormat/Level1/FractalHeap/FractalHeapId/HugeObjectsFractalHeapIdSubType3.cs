namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType3(
    H5DriverBase Driver,
    ulong Address,
    ulong Length
) : FractalHeapId
{
    public static async ValueTask<HugeObjectsFractalHeapIdSubType3> Decode(NativeReadContext context, H5DriverBase localDriver)
    {
        var (driver, superblock) = context;

        return new HugeObjectsFractalHeapIdSubType3(
            Driver: driver,
            Address: await superblock.ReadOffset(localDriver).ConfigureAwait(false),
            Length: await superblock.ReadLength(localDriver).ConfigureAwait(false)
        );
    }
    public override T Read<T>(Func<H5DriverBase, T> func)
    {
        Driver.SeekRelativeToBaseAddress((long)Address);
        return func(Driver);
    }
}