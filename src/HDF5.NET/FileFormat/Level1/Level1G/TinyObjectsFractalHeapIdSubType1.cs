namespace HDF5.NET
{
    public class TinyObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Constructors

        public TinyObjectsFractalHeapIdSubType1()
        {
            //
        }

        #endregion

        #region Properties

        public byte VersionTypeLength { get; set; }
        public byte[] Data { get; set; }

        #endregion
    }
}
