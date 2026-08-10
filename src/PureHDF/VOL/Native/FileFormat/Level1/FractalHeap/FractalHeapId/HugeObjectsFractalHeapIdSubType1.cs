using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal record class HugeObjectsFractalHeapIdSubType1(
    NativeReadContext Context,
    FractalHeapHeader HeapHeader,
    ulong BTree2Key
) : FractalHeapId
{
    internal static async ValueTask<HugeObjectsFractalHeapIdSubType1> Decode(
        NativeReadContext context,
        H5DriverBase localDriver,
        FractalHeapHeader header)
    {
        return new HugeObjectsFractalHeapIdSubType1(
            Context: context,
            HeapHeader: header,
            BTree2Key: await ReadUtils.ReadUlong(localDriver, header.HugeIdsSize).ConfigureAwait(false)
        );
    }

    public override async ValueTask<T> Read<T>(Func<H5DriverBase, ValueTask<T>> func)
    {
        var driver = Context.Driver;

        var records = await NativeCache
            .GetStructure(Context, HeapHeader.HugeObjectsBTree2Address, DecodeRecords)
            .ConfigureAwait(false);

        var hugeRecord = Array.Find(records, record => record.HugeObjectId == BTree2Key);
        driver.SeekRelativeToBaseAddress((long)hugeRecord.HugeObjectAddress);

        return await func(driver).ConfigureAwait(false);
    }

    // An array rather than the List<> this used to build: the result is now shared between concurrent
    // readers of the file, so "read-only from here on" should be the type's problem and not a
    // convention. NativeCache.GetStructure seeks to the address first, so the header decode below
    // starts where it expects to.
    private static async ValueTask<BTree2Record01[]> DecodeRecords(NativeReadContext context)
    {
        var hugeBTree2 = await BTree2Header<BTree2Record01>
            .Decode(context, DecodeRecord01)
            .ConfigureAwait(false);

        var records = new List<BTree2Record01>();

        await foreach (var record in hugeBTree2.EnumerateRecords(context, DecodeRecord01))
        {
            records.Add(record);
        }

        return [.. records];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<BTree2Record01> DecodeRecord01(NativeReadContext context) => await BTree2Record01.Decode(context).ConfigureAwait(false);
}
