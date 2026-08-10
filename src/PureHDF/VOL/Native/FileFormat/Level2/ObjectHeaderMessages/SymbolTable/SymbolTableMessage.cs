namespace PureHDF.VOL.Native;

// CONCURRENCY (retained message): see the note on LinkInfoMessage - this message is retained in
// ObjectHeader.HeaderMessages for the lifetime of the NativeObject, so it holds no
// NativeReadContext and caches nothing. Both accessors take the calling operation's context and
// decode a fresh LocalHeap / BTree1Node; those are transient and may capture the driver they were
// decoded through.
internal record class SymbolTableMessage(
    ulong BTree1Address,
    ulong LocalHeapAddress
) : Message
{
    public static async ValueTask<SymbolTableMessage> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var btree1Address = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var localHeapAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        return new SymbolTableMessage(
            BTree1Address: btree1Address,
            LocalHeapAddress: localHeapAddress
        );
    }

    public async ValueTask<LocalHeap> GetLocalHeap(NativeReadContext context)
    {
        context.Driver.SeekRelativeToBaseAddress((long)LocalHeapAddress);

        return await LocalHeap.Decode(context).ConfigureAwait(false);
    }

    public async ValueTask<BTree1Node<BTree1GroupKey>> GetBTree1(
        NativeReadContext context,
        Func<ValueTask<BTree1GroupKey>> decodeKey)
    {
        context.Driver.SeekRelativeToBaseAddress((long)BTree1Address);

        return await BTree1Node<BTree1GroupKey>.Decode(context, decodeKey).ConfigureAwait(false);
    }
}
