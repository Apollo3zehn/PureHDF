namespace HDF5.NET
{
    internal class HardLinkInfo : LinkInfo
    {
        #region Fields

// TODO: OK like this?
        private Superblock _superblock;

        #endregion

        #region Constructors

        public HardLinkInfo(H5BinaryReader reader, Superblock superblock)
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
