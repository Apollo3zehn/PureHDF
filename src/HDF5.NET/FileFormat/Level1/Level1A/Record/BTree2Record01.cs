namespace HDF5.NET
{
    public class BTree2Record01
    {
        #region Constructors

        public BTree2Record01()
        {
            //
        }

        #endregion

        #region Properties

        public ulong HugeObjectAddress { get; set; }
        public ulong HugeObjectLength { get; set; }
        public ulong HugeObjectId { get; set; }

        #endregion
    }
}
