namespace HDF5.NET
{
    internal class GlobalHeapId
    {
        #region Fields

        private H5Context _context;

        #endregion

        #region Constructors

        public GlobalHeapId(H5Context context)
        {
            _context = context;
        }

        public GlobalHeapId(H5Context context, H5BaseReader localReader)
        {
            var (reader, superblock) = context;
            _context = context;

            CollectionAddress = superblock.ReadOffset(localReader);
            ObjectIndex = localReader.ReadUInt32();
        }

        #endregion

        #region Properties

        public ulong CollectionAddress { get; set; }
        public uint ObjectIndex { get; set; }

        public GlobalHeapCollection Collection
        {
            get
            {
                // TODO: Because Global Heap ID gets a brand new reader (from the attribute), it cannot be reused here. Is this a good approach?
                return H5Cache.GetGlobalHeapObject(_context, CollectionAddress);
            }
        }

        #endregion
    }
}