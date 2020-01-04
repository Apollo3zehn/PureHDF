namespace HDF5.NET
{
    public abstract class BTree1GroupKey : BTree1Key
    {
        #region Constructors

        public BTree1GroupKey()
        {
            //
        }

        #endregion

        #region Properties

        public ulong LocalHeapByteOffset { get; set; }

        #endregion
    }
}
