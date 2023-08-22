using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType1(
    NativeReadContext Context,
    FractalHeapHeader HeapHeader,
    ulong BTree2Key
) : FractalHeapId
{
    internal static HugeObjectsFractalHeapIdSubType1 Decode(
        NativeReadContext context, 
        H5DriverBase localDriver, 
        FractalHeapHeader header)
    {
        return new HugeObjectsFractalHeapIdSubType1(
            Context: context,
            HeapHeader: header,
            BTree2Key: ReadUtils.ReadUlong(localDriver, header.HugeIdsSize)
        );
    }

    public override T Read<T>(
        Func<H5DriverBase, T> func, 
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        var driver = Context.Driver;

        // huge objects b-tree v2
        if (record01Cache is null)
        {
            driver.Seek((long)HeapHeader.HugeObjectsBTree2Address, SeekOrigin.Begin);
            var hugeBtree2 = BTree2Header<BTree2Record01>.Decode(Context, DecodeRecord01);
            record01Cache = hugeBtree2.EnumerateRecords().ToList();
        }

        var hugeRecord = record01Cache.FirstOrDefault(record => record.HugeObjectId == BTree2Key);
        driver.Seek((long)hugeRecord.HugeObjectAddress, SeekOrigin.Begin);

        return func(driver);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record01 DecodeRecord01() => BTree2Record01.Decode(Context);
}