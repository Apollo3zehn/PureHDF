using System.Diagnostics.CodeAnalysis;
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

    public override T Read<T>(
        Func<H5DriverBase, T> func,
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        var driver = Context.Driver;

        // huge objects b-tree v2
        if (record01Cache is null)
        {
            driver.SeekRelativeToBaseAddress((long)HeapHeader.HugeObjectsBTree2Address);

            // NOTE (async propagation): BTree2Header<T>.Decode/EnumerateRecords (out of
            // scope, BTree2Header.cs) are now async/IAsyncEnumerable, but this override
            // must match the abstract, synchronous `FractalHeapId.Read<T>` (out of scope),
            // whose `ref List<BTree2Record01> record01Cache` parameter can never itself
            // become async (CS1988 — the same constraint noted for BTree1Node.FoundDelegate).
            // Bridged synchronously here, mirroring the precedent already established in
            // NativeObject.EnumerateAttributeMessagesFromAttributeInfoMessage (out of scope).
            var hugeBtree2 = BTree2Header<BTree2Record01>.Decode(Context, DecodeRecord01).GetAwaiter().GetResult();

            var records = new List<BTree2Record01>();
            var recordEnumerator = hugeBtree2.EnumerateRecords().GetAsyncEnumerator();

            try
            {
                while (recordEnumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    records.Add(recordEnumerator.Current);
                }
            }
            finally
            {
                recordEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            record01Cache = records;
        }

        var hugeRecord = record01Cache.FirstOrDefault(record => record.HugeObjectId == BTree2Key);
        driver.SeekRelativeToBaseAddress((long)hugeRecord.HugeObjectAddress);

        return func(driver);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<BTree2Record01> DecodeRecord01() => await BTree2Record01.Decode(Context).ConfigureAwait(false);
}