namespace HDF5.NET
{
    public class ManagedObjectsFractalHeapId : FractalHeapId
    {
        #region Constructors

        public ManagedObjectsFractalHeapId()
        {
            //
        }

        #endregion

        #region Properties

        public byte VersionTypeLength { get; set; }
        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        #endregion
    }
}
