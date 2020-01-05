namespace HDF5.NET
{
    public class FractalHeapIndirectSectionDataRecord : SectionDataRecord
    {
        #region Constructors

        public FractalHeapIndirectSectionDataRecord()
        {
            //
        }

        #endregion

        #region Properties

        public ulong FractalHeapIndirectBlockOffset { get; set; }
        public ushort BlockStartRow { get; set; }
        public ushort BlockStartColumn { get; set; }
        public ushort BlockCount { get; set; }

        #endregion
    }
}
