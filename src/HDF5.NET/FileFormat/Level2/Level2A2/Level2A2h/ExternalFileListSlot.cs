namespace HDF5.NET
{
    public class ExternalFileListSlot
    {
        #region Constructors

        public ExternalFileListSlot()
        {
            //
        }

        #endregion

        #region Properties

        public ulong LocalHeapNameOffset { get; set; }
        public ulong ExternalDataFileOffset { get; set; }
        public ulong ExternalFileDataSize { get; set; }

        #endregion
    }
}
