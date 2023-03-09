namespace PureHDF
{
    internal class FractalHeapIndirectSectionDataRecord : SectionDataRecord
    {
        #region Constructors

        public FractalHeapIndirectSectionDataRecord(H5DriverBase driver)
        {
            // fractal heap indirect block offset
            // TODO: implement this correctly
            //FractalHeapIndirectBlockOffset = driver.ReadBytes(8);

            // block start row
            BlockStartRow = driver.ReadUInt16();

            // block start column
            BlockStartColumn = driver.ReadUInt16();

            // block count
            BlockCount = driver.ReadUInt16();
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
