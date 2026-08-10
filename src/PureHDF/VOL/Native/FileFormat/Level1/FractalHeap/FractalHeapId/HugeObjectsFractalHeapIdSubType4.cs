using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType4(
    ulong Address,
    ulong Length,
    uint FilterMask,
    ulong DeFilteredSize
) : FractalHeapId
{
    public static async ValueTask<HugeObjectsFractalHeapIdSubType4> Decode(
        Superblock superblock,
        H5DriverBase localDriver)
    {
        return new HugeObjectsFractalHeapIdSubType4(
            Address: await superblock.ReadOffset(localDriver).ConfigureAwait(false),
            Length: await superblock.ReadLength(localDriver).ConfigureAwait(false),
            FilterMask: await localDriver.ReadUInt32().ConfigureAwait(false),
            DeFilteredSize: await superblock.ReadLength(localDriver).ConfigureAwait(false)
        );
    }

    public override T Read<T>(
        Func<H5DriverBase, T> func,
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        throw new Exception("Filtered data is not yet supported.");
    }
}