namespace PureHDF
{
    internal abstract class ChunkedStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public byte Rank { get; set; }

        #endregion
    }
}
