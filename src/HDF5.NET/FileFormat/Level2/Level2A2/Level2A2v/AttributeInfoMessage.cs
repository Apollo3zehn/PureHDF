using System;
using System.IO;

namespace HDF5.NET
{
    public class AttributeInfoMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public AttributeInfoMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // flags
            this.Flags = (CreationOrderFlags)reader.ReadByte();

            // maximum creation index
            if (this.Flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
                this.MaximumCreationIndex = reader.ReadUInt16();

            // fractal heap address
            this.FractalHeapAddress = superblock.ReadOffset(reader);

            // b-tree 2 name index address
            this.BTree2NameIndexAddress = superblock.ReadOffset(reader);

            // b-tree 2 creation order index address
            if (this.Flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
                this.BTree2NameIndexAddress = superblock.ReadOffset(reader);
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

#warning Add object references to fractal heap and BTree2

        #endregion
    }
}
