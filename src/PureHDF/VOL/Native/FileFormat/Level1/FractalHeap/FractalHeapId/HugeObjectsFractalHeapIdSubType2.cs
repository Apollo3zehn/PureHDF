namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType2(
    NativeReadContext Context,
    FractalHeapHeader HeapHeader,
    ulong BTree2Key
) : HugeObjectsFractalHeapIdSubType1(Context, HeapHeader, BTree2Key)
{
    public static new async ValueTask<HugeObjectsFractalHeapIdSubType2> Decode(
        NativeReadContext context,
        H5DriverBase localDriver,
        FractalHeapHeader header)
    {
        return new HugeObjectsFractalHeapIdSubType2(
            Context: context,
            HeapHeader: header,
            BTree2Key: await ReadUtils.ReadUlong(localDriver, header.HugeIdsSize).ConfigureAwait(false)
        );
    }

    public override T Read<T>(Func<H5DriverBase, T> func)
    {
        throw new Exception("Filtered data is not yet supported.");
    }
}