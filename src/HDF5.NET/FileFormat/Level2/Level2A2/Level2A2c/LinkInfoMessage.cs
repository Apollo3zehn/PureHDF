using System;
using System.IO;

namespace HDF5.NET
{
    public class LinkInfoMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public LinkInfoMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // flags
            this.Flags = (CreationOrderFlags)reader.ReadByte();

            // maximum creation index
            if (this.Flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
                this.MaximumCreationIndex = reader.ReadUInt64();

            // fractal heap address
            this.FractalHeapAddress = superblock.ReadOffset();

            // BTree2 name index address
            this.BTree2NameIndexAddress = superblock.ReadOffset();

            // BTree2 creation order index address
            if (this.Flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
                this.BTree2CreationOrderIndexAddress = superblock.ReadOffset();
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

        #endregion
    }
}
