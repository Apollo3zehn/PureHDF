namespace HDF5.NET
{
    public class ContiguousStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ContiguousStoragePropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Size { get; set; }

        #endregion
    }
}
