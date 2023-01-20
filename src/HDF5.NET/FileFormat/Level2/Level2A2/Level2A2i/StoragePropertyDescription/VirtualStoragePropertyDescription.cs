namespace HDF5.NET
{
    internal class VirtualStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public VirtualStoragePropertyDescription(H5Context context)
        {
            var (reader, superblock) = context;
            
            // address
            Address = superblock.ReadOffset(reader);

            // index
            Index = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public uint Index { get; set; }

        #endregion
    }
}
