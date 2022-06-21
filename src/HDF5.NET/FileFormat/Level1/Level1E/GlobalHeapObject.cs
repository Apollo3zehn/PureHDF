using System;

namespace HDF5.NET
{
    internal class GlobalHeapObject : FileBlock
    {
        #region Constructors

        public GlobalHeapObject(H5BinaryReader reader, Superblock superblock) : base(reader)
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

        public ushort HeapObjectIndex { get; set; }
        public ushort ReferenceCount { get; set; }
        public byte[] ObjectData { get; set; }

        #endregion
    }
}
