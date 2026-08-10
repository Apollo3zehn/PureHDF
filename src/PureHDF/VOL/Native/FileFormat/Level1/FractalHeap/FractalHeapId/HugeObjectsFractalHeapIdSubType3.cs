using System.Diagnostics.CodeAnalysis;

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
    public override T Read<T>(
        Func<H5DriverBase, T> func,
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        Driver.SeekRelativeToBaseAddress((long)Address);
        return func(Driver);
    }
}