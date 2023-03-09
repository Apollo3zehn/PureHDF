namespace PureHDF
{
    internal class SymbolTableEntry
    {
        #region Constructors

        public SymbolTableEntry(H5Context context)
        {
            var (driver, superblock) = context;

            // link name offset
            LinkNameOffset = superblock.ReadOffset(driver);

            // object header address
            HeaderAddress = superblock.ReadOffset(driver);

            // cache type
            CacheType = (CacheType)driver.ReadUInt32();

            // reserved
            driver.ReadUInt32();

            // scratch pad
            var before = driver.Position;

            ScratchPad = CacheType switch
            {
                CacheType.NoCache => null,
                CacheType.ObjectHeader => new ObjectHeaderScratchPad(context),
                CacheType.SymbolicLink => new SymbolicLinkScratchPad(driver),
                _ => throw new NotSupportedException()
            };

            var after = driver.Position;
            var length = after - before;

            // read as many bytes as needed to read a total of 16 bytes, even if the scratch pad is not used
            driver.ReadBytes((int)(16 - length));
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
