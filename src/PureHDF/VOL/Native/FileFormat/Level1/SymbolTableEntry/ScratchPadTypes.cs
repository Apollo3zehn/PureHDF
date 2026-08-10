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

internal record class ObjectHeaderScratchPad(
    NativeReadContext Context,
    ulong BTree1Address,
    ulong NameHeapAddress
) : ScratchPad
{
    private LocalHeap _localHeap;
    private BTree1Node<BTree1GroupKey> _btree1Node;

    public static async ValueTask<ObjectHeaderScratchPad> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new ObjectHeaderScratchPad(
            Context: context,
            BTree1Address: await superblock.ReadLength(driver).ConfigureAwait(false),
            NameHeapAddress: await superblock.ReadLength(driver).ConfigureAwait(false)
        );
    }

    public async ValueTask<LocalHeap> GetLocalHeap()
    {
        if (_localHeap.Equals(default))
        {
            Context.Driver.SeekRelativeToBaseAddress((long)NameHeapAddress);
            _localHeap = await LocalHeap.Decode(Context).ConfigureAwait(false);
        }

        return _localHeap;
    }

    public async ValueTask<BTree1Node<BTree1GroupKey>> GetBTree1(Func<ValueTask<BTree1GroupKey>> decodeKey)
    {
        if (_btree1Node.Equals(default))
        {
            Context.Driver.SeekRelativeToBaseAddress((long)BTree1Address);
            _btree1Node = await BTree1Node<BTree1GroupKey>.Decode(Context, decodeKey).ConfigureAwait(false);
        }

        return _btree1Node;
    }
}