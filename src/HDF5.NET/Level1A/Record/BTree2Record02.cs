namespace HDF5.NET
{
    public class BTree2Record02
    {
        #region Constructors

        public BTree2Record02()
        {
            //
        }

        #endregion

        #region Properties

        public ulong HugeObjectAddress { get; set; }
        public ulong HugeObjectLength { get; set; }
        public uint FilterMask { get; set; }
        public ulong HugeObjectMemorySize { get; set; }
        public ulong HugeObjectId { get; set; }

        #endregion
    }
}
