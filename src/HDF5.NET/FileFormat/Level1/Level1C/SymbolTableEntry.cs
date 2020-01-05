namespace HDF5.NET
{
    public class SymbolTableEntry
    {
        #region Constructors

        public SymbolTableEntry()
        {
            //
        }

        #endregion

        #region Properties

        public ulong LinkNameOffset { get; set; }
        public ulong ObjectHeaderAddress { get; set; }
        public CacheType CacheType { get; set; }
        public ScratchPad ScratchPad { get; set; }

        #endregion
    }
}
