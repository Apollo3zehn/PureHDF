namespace HDF5.NET
{
    internal class HardLinkInfo : LinkInfo
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;

        #endregion

        #region Constructors

        public HardLinkInfo(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // object header address
            HeaderAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public ulong HeaderAddress { get; set; }

        #endregion
    }
}
