namespace PureHDF
{
    internal class HardLinkInfo : LinkInfo
    {
        #region Constructors

        public HardLinkInfo(H5Context context)
        {
            var (driver, superblock) = context;

            // object header address
            HeaderAddress = superblock.ReadOffset(driver);
        }

        #endregion

        #region Properties

        public ulong HeaderAddress { get; set; }

        #endregion
    }
}
