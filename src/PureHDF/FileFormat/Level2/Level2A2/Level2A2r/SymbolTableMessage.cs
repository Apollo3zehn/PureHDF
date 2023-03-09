namespace PureHDF
{
    internal class SymbolTableMessage : Message
    {
        #region Fields

        private H5Context _context;

        #endregion

        #region Constructors

        public SymbolTableMessage(H5Context context)
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
}
