using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    internal class LinkInfoMessage : Message
    {
        #region Fields

        private H5Context _context;
        private byte _version;

        #endregion

        #region Constructors

        public LinkInfoMessage(H5Context context)
        {
            _context = context;

            var (reader, superblock) = context;

            // version
            Version = reader.ReadByte();

            // flags
            Flags = (CreationOrderFlags)reader.ReadByte();

            // maximum creation index
            if (Flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
                MaximumCreationIndex = reader.ReadUInt64();

            // fractal heap address
            FractalHeapAddress = superblock.ReadOffset(reader);

            // BTree2 name index address
            BTree2NameIndexAddress = superblock.ReadOffset(reader);

            // BTree2 creation order index address
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
                    throw new FormatException($"Only version 0 instances of type {nameof(LinkInfoMessage)} are supported.");

                _version = value;
            }
        }

        public CreationOrderFlags Flags { get; set; }
        public ulong MaximumCreationIndex { get; set; }
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

        public BTree2Header<BTree2Record05> BTree2NameIndex
        {
            get
            {
                _context.Reader.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                return new BTree2Header<BTree2Record05>(_context, DecodeRecord05);
            }
        }

        public BTree2Header<BTree2Record06> BTree2CreationOrder
        {
            get
            {
                _context.Reader.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                return new BTree2Header<BTree2Record06>(_context, DecodeRecord06);
            }
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record05 DecodeRecord05() => new BTree2Record05(_context.Reader);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record06 DecodeRecord06() => new BTree2Record06(_context.Reader);

        #endregion
    }
}
