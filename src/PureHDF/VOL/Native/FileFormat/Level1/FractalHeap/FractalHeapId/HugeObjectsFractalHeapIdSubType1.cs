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
        [AllowNull] ref BTree2Header<BTree2Record01> record01Cache)
    {
        var driver = Context.Driver;

        // huge objects b-tree v2
        if (record01Cache is null)
        {
            driver.SeekRelativeToBaseAddress((long)HeapHeader.HugeObjectsBTree2Address);
            record01Cache = BTree2Header<BTree2Record01>.Decode(Context, DecodeRecord01);
        }

        var success = record01Cache.TryFindRecord(out var hugeRecord, record => BTree2Key.CompareTo(record.HugeObjectId));

        if (!success)
            throw new Exception("Could not find huge fractal heap object.");

        driver.SeekRelativeToBaseAddress((long)hugeRecord.HugeObjectAddress);

        return func(driver);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record01 DecodeRecord01() => BTree2Record01.Decode(Context);
}