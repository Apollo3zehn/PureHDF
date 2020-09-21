namespace HDF5.NET
{
    public abstract class ChunkedStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Dimensionality { get; set; }
        public ulong Address { get; set; }

        #endregion
    }
}
