namespace HDF5.NET
{
    public class VirtualStoragePropertyDescription : IndexingInformation
    {
        #region Constructors

        public VirtualStoragePropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public uint Index { get; set; }

        #endregion
    }
}
