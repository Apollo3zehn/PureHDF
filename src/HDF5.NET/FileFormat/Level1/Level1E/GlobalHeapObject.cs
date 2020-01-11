using System.IO;

namespace HDF5.NET
{
    public class GlobalHeapObject : FileBlock
    {
        #region Constructors

        public GlobalHeapObject(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // heap object index
            this.HeapObjectIndex = reader.ReadUInt16();

            // reference count
            this.ReferenceCount = reader.ReadUInt16();

            // reserved
            reader.ReadBytes(4);

            // object size
            this.ObjectSize = superblock.ReadLength();

            // object data
            this.ObjectData = reader.ReadBytes((int)this.ObjectSize);
            reader.ReadBytes((int)(this.ObjectSize % 8));
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
