namespace PureHDF.VOL.Native;

internal abstract record class ScratchPad
{
    //
}

internal record class SymbolicLinkScratchPad(
    uint LinkValueOffset
) : ScratchPad
{
    public static async ValueTask<SymbolicLinkScratchPad> Decode(H5DriverBase driver)
    {
        return new SymbolicLinkScratchPad(
            LinkValueOffset: await driver.ReadUInt32().ConfigureAwait(false)
        );
    }
}

// CONCURRENCY (retained): this scratch pad is attached to a NativeNamedReference
// (NativeNamedReference.ScratchPad) and handed to the caller, and a NativeGroup built from that
// reference keeps it for its whole lifetime - so it OUTLIVES the navigation operation that decoded
// it. It therefore holds no NativeReadContext and caches nothing; see the note on LinkInfoMessage
// for why a captured per-operation driver would be a correctness bug rather than merely stale.
internal record class ObjectHeaderScratchPad(
    ulong BTree1Address,
    ulong NameHeapAddress
) : ScratchPad
{
    public static async ValueTask<ObjectHeaderScratchPad> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new ObjectHeaderScratchPad(
            BTree1Address: await superblock.ReadLength(driver).ConfigureAwait(false),
            NameHeapAddress: await superblock.ReadLength(driver).ConfigureAwait(false)
        );
    }

    public ValueTask<LocalHeap> GetLocalHeap(NativeReadContext context)
    {
        return NativeCache.GetStructure(context, NameHeapAddress, LocalHeap.Decode);
    }

    public ValueTask<BTree1Node<BTree1GroupKey>> GetBTree1(
        NativeReadContext context,
        DecodeKeyDelegate<BTree1GroupKey> decodeKey)
    {
        return NativeCache.GetStructure(
            context,
            BTree1Address,
            decodeKey,
            static (c, dk) => BTree1Node<BTree1GroupKey>.Decode(c, dk));
    }
}
