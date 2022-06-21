using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    internal class LinkInfoMessage : Message
    {
        #region Fields

        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public LinkInfoMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

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
                Reader.Seek((long)FractalHeapAddress, SeekOrigin.Begin);
                return new FractalHeapHeader(Reader, _superblock);
            }
        }

        public BTree2Header<BTree2Record05> BTree2NameIndex
        {
            get
            {
                Reader.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                return new BTree2Header<BTree2Record05>(Reader, _superblock, DecodeRecord05);
            }
        }

        public BTree2Header<BTree2Record06> BTree2CreationOrder
        {
            get
            {
                Reader.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                return new BTree2Header<BTree2Record06>(Reader, _superblock, DecodeRecord06);
            }
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record05 DecodeRecord05() => new BTree2Record05(Reader);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record06 DecodeRecord06() => new BTree2Record06(Reader);

        #endregion
    }
}
