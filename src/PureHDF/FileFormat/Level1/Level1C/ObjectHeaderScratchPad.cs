namespace PureHDF
{
    internal class ObjectHeaderScratchPad : ScratchPad
    {
        #region Fields

        private H5Context _context;

        #endregion

        #region Constructors

        public ObjectHeaderScratchPad(H5Context context)
        {
            var (reader, superblock) = context;
            _context = context;

            BTree1Address = superblock.ReadLength(reader);
            NameHeapAddress = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong BTree1Address { get; set; }
        public ulong NameHeapAddress { get; set; }

        public LocalHeap LocalHeap
        {
            get
            {
                _context.Reader.Seek((long)NameHeapAddress, SeekOrigin.Begin);
                return new LocalHeap(_context);
            }
        }

        #endregion

        #region Methods

        public BTree1Node<BTree1GroupKey> GetBTree1(Func<BTree1GroupKey> decodeKey)
        {
            _context.Reader.Seek((long)BTree1Address, SeekOrigin.Begin);
            return new BTree1Node<BTree1GroupKey>(_context, decodeKey);
        }

        #endregion
    }
}
