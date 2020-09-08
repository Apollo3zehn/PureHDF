namespace HDF5.NET
{
    public class FractalHeapIndirectSectionDataRecord : SectionDataRecord
    {
        #region Constructors

        public FractalHeapIndirectSectionDataRecord(H5BinaryReader reader) : base(reader)
        {
            // fractal heap indirect block offset
#warning implement this correctly
            //this.FractalHeapIndirectBlockOffset = reader.ReadBytes(8);

            // block start row
            this.BlockStartRow = reader.ReadUInt16();

            // block start column
            this.BlockStartColumn = reader.ReadUInt16();

            // block count
            this.BlockCount = reader.ReadUInt16();
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
