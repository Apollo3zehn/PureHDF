namespace HDF5.NET
{
    public class SymbolTableMessage
    {
        #region Constructors

        public SymbolTableMessage()
        {
            //
        }

        #endregion

        #region Properties

        public ulong BTree1Address { get; set; }
        public ulong LocalHeapAddress { get; set; }

        #endregion
    }
}
