namespace HDF5.NET
{
    public class FractalHeapDirectBlock
    {
        #region Constructors

        public FractalHeapDirectBlock()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ulong HeapHeaderAddress { get; set; }
        public ulong BlockOffset { get; set; }
        public uint Checksum { get; set; }
        public byte[] ObjectData { get; set; }

        #endregion
    }
}
