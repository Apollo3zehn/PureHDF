namespace PureHDF.VOL.Native;

internal class ObjectHeaderScratchPad : ScratchPad
{
    #region Fields

    private NativeContext _context;

    #endregion

    #region Constructors

    public ObjectHeaderScratchPad(NativeContext context)
    {
        var (driver, superblock) = context;
        _context = context;

        BTree1Address = superblock.ReadLength(driver);
        NameHeapAddress = superblock.ReadLength(driver);
    }

    #endregion

    #region Properties

    public ulong BTree1Address { get; set; }
    public ulong NameHeapAddress { get; set; }

    public LocalHeap LocalHeap
    {
        get
        {
            _context.Driver.Seek((long)NameHeapAddress, SeekOrigin.Begin);
            return new LocalHeap(_context);
        }
    }

    #endregion

    #region Methods

    public BTree1Node<BTree1GroupKey> GetBTree1(Func<BTree1GroupKey> decodeKey)
    {
        _context.Driver.Seek((long)BTree1Address, SeekOrigin.Begin);
        return new BTree1Node<BTree1GroupKey>(_context, decodeKey);
    }

    #endregion
}