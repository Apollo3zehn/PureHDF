namespace HDF5.NET
{
    public class FractalHeapSharedMessageRecord
    {
        #region Constructors

        public FractalHeapSharedMessageRecord()
        {
            //
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }
        public uint HashValue { get; set; }
        public uint ReferenceCount { get; set; }
        public FractalHeapId FractalHeapId { get; set; }

        #endregion
    }
}
