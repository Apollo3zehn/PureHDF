namespace HDF5.NET
{
    public class LocalHeap
    {
        #region Constructors

        public LocalHeap()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ulong DataSegmentSize { get; set; }
        public ulong FreeListHeadOffset { get; set; }
        public ulong DataSegmentAddress { get; set; }

        #endregion
    }
}
