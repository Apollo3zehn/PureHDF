namespace PureHDF
{
    internal class FractalHeapIndirectSectionDataRecord : SectionDataRecord
    {
        #region Constructors

        public FractalHeapIndirectSectionDataRecord(H5BaseReader reader)
        {
            // fractal heap indirect block offset
            // TODO: implement this correctly
            //FractalHeapIndirectBlockOffset = reader.ReadBytes(8);

            // block start row
            BlockStartRow = reader.ReadUInt16();

            // block start column
            BlockStartColumn = reader.ReadUInt16();

            // block count
            BlockCount = reader.ReadUInt16();
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
