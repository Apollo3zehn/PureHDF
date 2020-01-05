namespace HDF5.NET
{
    public class BTreeKValuesMessage
    {
        #region Constructors

        public BTreeKValuesMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ushort IndexedStorageInternalNodeK { get; set; }
        public ushort GroupInternalNodeK { get; set; }
        public ushort GroupLeafNodeK { get; set; }

        #endregion
    }
}
