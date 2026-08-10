namespace PureHDF.VOL.Native;

// CONCURRENCY (retained message): see the note on LinkInfoMessage - this message is retained in
// ObjectHeader.HeaderMessages for the lifetime of the NativeObject, so it holds no
// NativeReadContext and caches nothing itself. Both accessors take the calling operation's context.
// Caching lives in NativeCache instead, keyed per file and per address, which is what keeps a
// repeated by-name lookup from re-decoding storage it has already walked.
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

    public ValueTask<LocalHeap> GetLocalHeap(NativeReadContext context)
    {
        return NativeCache.GetStructure(context, LocalHeapAddress, LocalHeap.Decode);
    }

    public async ValueTask<BTree1Node<BTree1GroupKey>> GetBTree1(
        NativeReadContext context,
        Func<ValueTask<BTree1GroupKey>> decodeKey)
    {
        context.Driver.SeekRelativeToBaseAddress((long)BTree1Address);

        return await BTree1Node<BTree1GroupKey>.Decode(context, decodeKey).ConfigureAwait(false);
    }
}
