using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType3(
    H5DriverBase Driver,
    ulong Address,
    ulong Length
) : FractalHeapId
{
    public static HugeObjectsFractalHeapIdSubType3 Decode(NativeContext context, H5DriverBase localDriver)
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
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        Driver.Seek((long)Address, SeekOrigin.Begin);
        return func(Driver);
    }
}