using System;

namespace HDF5.NET
{
    public class GlobalHeapObject : FileBlock
    {
        #region Constructors

        public GlobalHeapObject(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // heap object index
            this.HeapObjectIndex = reader.ReadUInt16();

            // reference count
            this.ReferenceCount = reader.ReadUInt16();

            // reserved
            reader.ReadBytes(4);

            // object size
            var objectSize = superblock.ReadLength(reader);

            // object data
            this.ObjectData = reader.ReadBytes((int)objectSize);

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
