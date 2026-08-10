namespace PureHDF.VOL.Native;

internal record class SymbolTableMessage(
    NativeReadContext Context,
    ulong BTree1Address,
    ulong LocalHeapAddress
) : Message
{
    private LocalHeap _localHeap;
    private BTree1Node<BTree1GroupKey> _bTree1;

    public static async ValueTask<SymbolTableMessage> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var btree1Address = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var localHeapAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        return new SymbolTableMessage(
            Context: context,
            BTree1Address: btree1Address,
            LocalHeapAddress: localHeapAddress
        );
    }

    // NOTE (async propagation): was a property; C# has no async property getters,
    // so this became a method with the same name pattern used elsewhere in this
    // wave (see ScratchPadTypes.cs). Callers outside this file (NativeGroup.cs)
    // need updating — see report.
    public async ValueTask<LocalHeap> GetLocalHeap()
    {
        if (_localHeap.Equals(default))
        {
            Context.Driver.SeekRelativeToBaseAddress((long)LocalHeapAddress);
            _localHeap = await LocalHeap.Decode(Context).ConfigureAwait(false);
        }

        return _localHeap;
    }

    // NOTE (async propagation): kept the existing method name; body now awaits.
    // Callers outside this file (NativeGroup.cs) need updating — see report.
    public async ValueTask<BTree1Node<BTree1GroupKey>> GetBTree1(Func<ValueTask<BTree1GroupKey>> decodeKey)
    {
        if (_bTree1.Equals(default))
        {
            Context.Driver.SeekRelativeToBaseAddress((long)BTree1Address);
            _bTree1 = await BTree1Node<BTree1GroupKey>.Decode(Context, decodeKey).ConfigureAwait(false);
        }

        return _bTree1;
    }
}