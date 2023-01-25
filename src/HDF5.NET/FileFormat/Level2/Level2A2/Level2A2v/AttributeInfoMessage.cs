using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    internal class AttributeInfoMessage : Message
    {
        #region Fields

        private H5Context _context;
        private byte _version;

        #endregion

        #region Constructors

        public AttributeInfoMessage(H5Context context)
        {
            var (reader, superblock) = context;
            _context = context;

            // version
            Version = reader.ReadByte();

            // flags
            Flags = (CreationOrderFlags)reader.ReadByte();

            // maximum creation index
            if (Flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
                MaximumCreationIndex = reader.ReadUInt16();

            // fractal heap address
            FractalHeapAddress = superblock.ReadOffset(reader);

            // b-tree 2 name index address
            BTree2NameIndexAddress = superblock.ReadOffset(reader);

            // b-tree 2 creation order index address
            if (Flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
                BTree2CreationOrderIndexAddress = superblock.ReadOffset(reader);
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
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(AttributeInfoMessage)} are supported.");

                _version = value;
            }
        }

        public CreationOrderFlags Flags { get; set; }
        public ushort MaximumCreationIndex { get; set; }
        public ulong FractalHeapAddress { get; set; }
        public ulong BTree2NameIndexAddress { get; set; }
        public ulong BTree2CreationOrderIndexAddress { get; set; }

        public FractalHeapHeader FractalHeap
        {
            get
            {
                _context.Reader.Seek((long)FractalHeapAddress, SeekOrigin.Begin);
                return new FractalHeapHeader(_context);
            }
        }

        public BTree2Header<BTree2Record08> BTree2NameIndex
        {
            get
            {
                _context.Reader.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                return new BTree2Header<BTree2Record08>(_context, DecodeRecord08);
            }
        }

        public BTree2Header<BTree2Record09> BTree2CreationOrder
        {
            get
            {
                _context.Reader.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                return new BTree2Header<BTree2Record09>(_context, DecodeRecord09);
            }
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record08 DecodeRecord08() => new(_context.Reader);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record09 DecodeRecord09() => new(_context.Reader);

        #endregion
    }
}
