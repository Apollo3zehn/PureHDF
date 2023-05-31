namespace PureHDF.VOL.Native;

internal class SymbolTableMessage : Message
{
    #region Fields

    private readonly NativeContext _context;

    #endregion

    #region Constructors

    public SymbolTableMessage(NativeContext context)
    {
        var (driver, superblock) = context;
        _context = context;

        BTree1Address = superblock.ReadOffset(driver);
        LocalHeapAddress = superblock.ReadOffset(driver);
    }

    #endregion

    #region Properties

    public ulong BTree1Address { get; set; }
    public ulong LocalHeapAddress { get; set; }

    public LocalHeap LocalHeap
    {
        get
        {
            _context.Driver.Seek((long)LocalHeapAddress, SeekOrigin.Begin);
            return LocalHeap.Decode(_context);
        }
    }

    #endregion

    #region Methods

    public BTree1Node<BTree1GroupKey> GetBTree1(Func<BTree1GroupKey> decodeKey)
    {
        _context.Driver.Seek((long)BTree1Address, SeekOrigin.Begin);
        return BTree1Node<BTree1GroupKey>.Decode(_context, decodeKey);
    }

    #endregion
}