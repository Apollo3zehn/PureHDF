namespace HDF5.NET
{
    internal class GlobalHeapObject
    {
        #region Fields

        private static byte[] _emptyByteArray = new byte[0];

        #endregion

        #region Constructors

        public GlobalHeapObject(H5BinaryReader reader, Superblock superblock)
        {
            // heap object index
            HeapObjectIndex = reader.ReadUInt16();

            if (HeapObjectIndex == 0)
                return;

            // reference count
            ReferenceCount = reader.ReadUInt16();

            // reserved
            reader.ReadBytes(4);

            // object size
            var objectSize = superblock.ReadLength(reader);

            // object data
            ObjectData = reader.ReadBytes((int)objectSize);

            var paddedSize = (int)(Math.Ceiling(objectSize / 8.0) * 8);
            var remainingSize = paddedSize - (int)objectSize;
            reader.ReadBytes(remainingSize);
        }

        #endregion

        #region Properties

        public ushort HeapObjectIndex { get; }
        public ushort ReferenceCount { get; }
        public byte[] ObjectData { get; } = _emptyByteArray;

        #endregion
    }
}
