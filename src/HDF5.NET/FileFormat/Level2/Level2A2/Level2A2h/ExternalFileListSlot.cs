namespace HDF5.NET
{
    internal class ExternalFileListSlot
    {
        #region Constructors

        public ExternalFileListSlot(H5Context context)
        {
            var (reader, superblock) = context;
            
            // name heap offset
            NameHeapOffset = superblock.ReadLength(reader);

            // offset
            Offset = superblock.ReadLength(reader);

            // size
            Size = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong NameHeapOffset { get; set; }
        public ulong Offset { get; set; }
        public ulong Size { get; set; }

        #endregion
    }
}
