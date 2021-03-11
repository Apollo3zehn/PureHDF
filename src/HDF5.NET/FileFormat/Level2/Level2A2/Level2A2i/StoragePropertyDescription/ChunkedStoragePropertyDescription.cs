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

        public byte Rank { get; set; }

        #endregion
    }
}
