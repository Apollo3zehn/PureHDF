namespace HDF5.NET
{
    public class GlobalHeapObject
    {
        #region Constructors

        public GlobalHeapObject()
        {
            //
        }

        #endregion

        #region Properties

        public ushort HeapObjectIndex { get; set; }
        public ushort ReferenceCount { get; set; }
        public ulong ObjectSize { get; set; }
        public byte[] ObjectData { get; set; }

        #endregion
    }
}
