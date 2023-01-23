namespace HDF5.NET
{
    internal class SymbolTableEntry
    {
        #region Fields

        Superblock _superblock;

        #endregion

        #region Constructors

        public SymbolTableEntry(H5Context context)
        {
            var (reader, superblock) = context;
            _superblock = superblock;

            // link name offset
            LinkNameOffset = superblock.ReadOffset(reader);
            
            // object header address
            HeaderAddress = superblock.ReadOffset(reader);

            // cache type
            CacheType = (CacheType)reader.ReadUInt32();

            // reserved
            reader.ReadUInt32();

            // scratch pad
            var before = reader.Position;

            ScratchPad = CacheType switch
            {
                CacheType.NoCache => null,
                CacheType.ObjectHeader => new ObjectHeaderScratchPad(context),
                CacheType.SymbolicLink => new SymbolicLinkScratchPad(reader),
                _ => throw new NotSupportedException()
            };

            var after = reader.Position;
            var length = after - before;

            // read as many bytes as needed to read a total of 16 bytes, even if the scratch pad is not used
            reader.ReadBytes((int)(16 - length));
        }

        #endregion

        #region Properties

        public ulong LinkNameOffset { get; set; }
        public ulong HeaderAddress { get; set; }
        public CacheType CacheType { get; set; }
        public ScratchPad? ScratchPad { get; set; }

        #endregion
    }
}
