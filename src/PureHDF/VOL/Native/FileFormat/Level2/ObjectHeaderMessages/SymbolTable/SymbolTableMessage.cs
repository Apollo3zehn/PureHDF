namespace PureHDF.VOL.Native;

internal record class SymbolTableMessage(
    NativeContext Context,
    ulong BTree1Address,
    ulong LocalHeapAddress
) : Message
{
    private LocalHeap _localHeap;
    private BTree1Node<BTree1GroupKey> _bTree1;

    public static SymbolTableMessage Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        return new SymbolTableMessage(
            Context: context,
            BTree1Address: superblock.ReadOffset(driver),
            LocalHeapAddress: superblock.ReadOffset(driver)
        );
    }

    public LocalHeap LocalHeap
    {
        get
        {
            if (_localHeap.Equals(default))
            {
                Context.Driver.Seek((long)LocalHeapAddress, SeekOrigin.Begin);
                _localHeap = LocalHeap.Decode(Context);
            }

            return _localHeap;
        }
    }

    public BTree1Node<BTree1GroupKey> GetBTree1(Func<BTree1GroupKey> decodeKey)
    {
        if (_bTree1.Equals(default))
        {
            Context.Driver.Seek((long)BTree1Address, SeekOrigin.Begin);
            _bTree1 = BTree1Node<BTree1GroupKey>.Decode(Context, decodeKey);
        }

        return _bTree1;
    }
}