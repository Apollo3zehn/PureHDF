using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType2(
    NativeReadContext Context,
    FractalHeapHeader HeapHeader,
    ulong BTree2Key
) : HugeObjectsFractalHeapIdSubType1(Context, HeapHeader, BTree2Key)
{
    public static new HugeObjectsFractalHeapIdSubType2 Decode(
        NativeReadContext context, 
        H5DriverBase localDriver, 
        FractalHeapHeader header)
    {
        return new HugeObjectsFractalHeapIdSubType2(
            Context: context,
            HeapHeader: header,
            BTree2Key: ReadUtils.ReadUlong(localDriver, header.HugeIdsSize)
        );
    }

    public override T Read<T>(
        Func<H5DriverBase, T> func, 
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        throw new Exception("Filtered data is not yet supported.");
    }
}