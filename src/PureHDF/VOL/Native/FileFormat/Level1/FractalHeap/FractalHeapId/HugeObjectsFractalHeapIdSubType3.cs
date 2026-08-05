using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType3(
    H5DriverBase Driver,
    ulong Address,
    ulong Length
) : FractalHeapId
{
    public static HugeObjectsFractalHeapIdSubType3 Decode(NativeReadContext context, H5DriverBase localDriver)
    {
        var (driver, superblock) = context;

        return new HugeObjectsFractalHeapIdSubType3(
            Driver: driver,
            Address: superblock.ReadOffset(localDriver),
            Length: superblock.ReadLength(localDriver)
        );
    }
    public override T Read<T>(
        Func<H5DriverBase, T> func,
        [AllowNull] ref BTree2Header<BTree2Record01> record01Cache)
    {
        Driver.SeekRelativeToBaseAddress((long)Address);
        return func(Driver);
    }
}