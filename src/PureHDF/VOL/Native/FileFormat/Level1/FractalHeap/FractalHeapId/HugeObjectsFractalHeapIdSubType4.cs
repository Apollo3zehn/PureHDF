using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType4(
    ulong Address,
    ulong Length,
    uint FilterMask,
    ulong DeFilteredSize
) : FractalHeapId
{
    public static HugeObjectsFractalHeapIdSubType4 Decode(
        Superblock superblock, 
        H5DriverBase localDriver)
    {
        return new HugeObjectsFractalHeapIdSubType4(
            Address: superblock.ReadOffset(localDriver),
            Length: superblock.ReadLength(localDriver),
            FilterMask: localDriver.ReadUInt32(),
            DeFilteredSize: superblock.ReadLength(localDriver)
        );
    }

    public override T Read<T>(
        Func<H5DriverBase, T> func, 
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        throw new Exception("Filtered data is not yet supported.");
    }
}