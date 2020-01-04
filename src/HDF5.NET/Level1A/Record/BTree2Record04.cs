namespace HDF5.NET
{
    public class BTree2Record04
    {
        #region Constructors

        public BTree2Record04()
        {
            //
        }

        #endregion

        #region Properties

        public ulong HugeObjectAddress { get; set; }
        public ulong HugeObjectLength { get; set; }
        public uint FilterMask { get; set; }
        public ulong HugeObjectMemorySize { get; set; }

        #endregion
    }
}
