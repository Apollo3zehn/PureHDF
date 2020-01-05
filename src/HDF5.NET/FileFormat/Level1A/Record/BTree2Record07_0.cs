namespace HDF5.NET
{
    public class BTree2Record07_0
    {
        #region Constructors

        public BTree2Record07_0()
        {
            //
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }
        public uint Hash { get; set; }
        public uint ReferenceCount { get; set; }
        public ulong HeapId { get; set; }

        #endregion
    }
}
