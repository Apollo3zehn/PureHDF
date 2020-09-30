namespace HDF5.NET
{
    internal struct H5Context
    {
        #region Constructors

        public H5Context(H5BinaryReader reader, Superblock superblock)           
        {
            this.Reader = reader;
            this.Superblock = superblock;
        }

        #endregion

        #region Properties

        public H5BinaryReader Reader { get; }

        public Superblock Superblock { get; }

        #endregion
    }
}
