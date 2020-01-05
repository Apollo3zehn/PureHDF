namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType1And2 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType1And2()
        {
            //
        }

        #endregion

        #region Properties

        public byte VersionType { get; set; }
        public ulong BTree2Key { get; set; }

        #endregion
    }
}
