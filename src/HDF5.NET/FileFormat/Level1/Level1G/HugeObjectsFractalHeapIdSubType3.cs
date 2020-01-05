namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType3 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType3()
        {
            //
        }

        #endregion

        #region Properties

        public byte VersionType { get; set; }
        public ulong Address { get; set; }
        public ulong Length { get; set; }

        #endregion
    }
}
