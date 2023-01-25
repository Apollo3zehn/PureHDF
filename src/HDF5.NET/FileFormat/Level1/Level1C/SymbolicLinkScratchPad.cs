namespace HDF5.NET
{
    internal class SymbolicLinkScratchPad : ScratchPad
    {
        #region Constructors

        public SymbolicLinkScratchPad(H5BaseReader reader)
        {
            LinkValueOffset = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public uint LinkValueOffset { get; set; }

        #endregion
    }
}
