namespace HDF5.NET
{
    public class GlobalHeapId
    {
        #region Constructors

        public GlobalHeapId()
        {
            //
        }

        #endregion

        #region Properties

        public ulong CollectionAddress { get; set; }
        public uint ObjectIndex { get; set; }

        #endregion
    }
}
