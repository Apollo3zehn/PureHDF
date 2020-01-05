namespace HDF5.NET
{
    public class ObjectHeaderScratchPad : ScratchPad
    {
        #region Constructors

        public ObjectHeaderScratchPad()
        {
            //
        }

        #endregion

        #region Properties

        public ulong BTreeAddress { get; set; }
        public ulong NameHeapAddress { get; set; }

        #endregion
    }
}
