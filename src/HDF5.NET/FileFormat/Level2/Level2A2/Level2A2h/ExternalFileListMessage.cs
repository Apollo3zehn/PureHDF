namespace HDF5.NET
{
    internal class ExternalFileListMessage : Message
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public ExternalFileListMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // version
            Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

#warning Its value must be at least as large as the value contained in the Used Slots field.
            // allocated slot count
            AllocatedSlotCount = reader.ReadUInt16();

            // used slot count
            UsedSlotCount = reader.ReadUInt16();

            // heap address
            HeapAddress = superblock.ReadOffset(reader);

            // slot definitions
            SlotDefinitions = new ExternalFileListSlot[UsedSlotCount];

            for (int i = 0; i < UsedSlotCount; i++)
            {
                SlotDefinitions[i] = new ExternalFileListSlot(reader, superblock);
            }
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(ExternalFileListMessage)} are supported.");

                _version = value;
            }
        }

        public ushort AllocatedSlotCount { get; set; }
        public ushort UsedSlotCount { get; set; }
        public ulong HeapAddress { get; set; }
        public ExternalFileListSlot[] SlotDefinitions { get; set; }

        public LocalHeap Heap
        {
            get
            {
                Reader.Seek((long)HeapAddress, SeekOrigin.Begin);
                return new LocalHeap(Reader, _superblock);
            }
        }

        #endregion
    }
}
