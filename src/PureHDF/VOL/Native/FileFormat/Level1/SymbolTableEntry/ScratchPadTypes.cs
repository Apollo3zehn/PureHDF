namespace PureHDF.VOL.Native;

internal abstract record class ScratchPad
{
    //
}

internal record class SymbolicLinkScratchPad(
    uint LinkValueOffset
) : ScratchPad
{
    public static SymbolicLinkScratchPad Decode(H5DriverBase driver)
    {
        return new SymbolicLinkScratchPad(
            LinkValueOffset: driver.ReadUInt32()
        );
    }
}

internal record class ObjectHeaderScratchPad(
    NativeContext Context,
    ulong BTree1Address,
    ulong NameHeapAddress
) : ScratchPad
{
    private LocalHeap? _localHeap;
    private BTree1Node<BTree1GroupKey> _btree1Node;

    public static ObjectHeaderScratchPad Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        return new ObjectHeaderScratchPad(
            Context: context,
            BTree1Address: superblock.ReadLength(driver),
            NameHeapAddress: superblock.ReadLength(driver)
        );
    }

    public LocalHeap LocalHeap
    {
        get
        {
            if (_localHeap is null)
            {
                Context.Driver.Seek((long)NameHeapAddress, SeekOrigin.Begin);
                _localHeap = new LocalHeap(Context);
            }

            return _localHeap;
        }
    }

    public BTree1Node<BTree1GroupKey> GetBTree1(Func<BTree1GroupKey> decodeKey)
    {
        if (_btree1Node.Equals(default))
        {
            Context.Driver.Seek((long)BTree1Address, SeekOrigin.Begin);
            _btree1Node = BTree1Node<BTree1GroupKey>.Decode(Context, decodeKey);
        }

        return _btree1Node;
    }
}