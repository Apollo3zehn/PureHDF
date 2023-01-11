namespace HDF5.NET
{
    internal class HardLinkInfo : LinkInfo
    {
        #region Constructors

        public HardLinkInfo(H5BinaryReader reader, Superblock superblock)
        {
            // object header address
            HeaderAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public ulong HeaderAddress { get; set; }

        #endregion
    }
}
