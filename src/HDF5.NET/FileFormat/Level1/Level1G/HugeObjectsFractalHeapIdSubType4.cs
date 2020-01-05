namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType4 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType4()
        {
            //
        }

        #endregion

        #region Properties

        public byte VersionType { get; set; }
        public ulong Address { get; set; }
        public ulong Length { get; set; }
        public uint FilterMask { get; set; }
        public ulong DeFilteredSize { get; set; }

        #endregion
    }
}
