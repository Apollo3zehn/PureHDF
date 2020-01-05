namespace HDF5.NET
{
    public class TinyObjectsFractalHeapIdSubType2 : FractalHeapId
    {
        #region Constructors

        public TinyObjectsFractalHeapIdSubType2()
        {
            //
        }

        #endregion

        #region Properties

        public byte VersionTypeLength { get; set; }
        public byte ExtendedLength { get; set; }
        public byte[] Data { get; set; }

        #endregion
    }
}
