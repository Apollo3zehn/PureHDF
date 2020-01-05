namespace HDF5.NET
{
    public class BTree2Record08
    {
        #region Constructors

        public BTree2Record08()
        {
            //
        }

        #endregion

        #region Properties

        public uint HeapId { get; set; }
        public byte MessageFlags { get; set; }
        public uint CreationOrder { get; set; }
        public uint NameHash { get; set; }

        #endregion
    }
}
