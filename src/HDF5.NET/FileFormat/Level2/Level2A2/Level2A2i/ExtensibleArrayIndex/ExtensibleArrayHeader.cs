using System;
using System.Text;

namespace HDF5.NET
{
    internal class ExtensibleArrayHeader : FileBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ExtensibleArrayHeader(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, ExtensibleArrayHeader.Signature);

            // version
            this.Version = reader.ReadByte();

            // client ID
            this.ClientID = (ClientID)reader.ReadByte();

            // byte fields
            this.ElementSize = reader.ReadByte();
            this.ExtensibleArrayMaximumNumberOfElementsBits = reader.ReadByte();
            this.IndexBlockElementsCount = reader.ReadByte();
            this.DataBlockMininumElementsCount = reader.ReadByte();
            this.SecondaryBlockMinimumDataBlockPointerCount = reader.ReadByte();
            this.DataBlockPageMaximumNumberOfElementsBits = reader.ReadByte();

            // length fields
            this.SecondaryBlocksCount = superblock.ReadLength(reader);
            this.SecondaryBlocksSize = superblock.ReadLength(reader);
            this.DataBlocksCount = superblock.ReadLength(reader);
            this.DataBlocksSize = superblock.ReadLength(reader);
            this.MaximumIndexSet = superblock.ReadLength(reader);
            this.ElementsCount = superblock.ReadLength(reader);

            // index block address
            this.IndexBlockAddress = superblock.ReadOffset(reader);

            // checksum
            this.Checksum = reader.ReadUInt32();

            // initialize
            this.Initialize();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EAHD");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArrayHeader)} are supported.");

                _version = value;
            }
        }

        public ClientID ClientID { get; }

        public byte ElementSize { get; }

        public byte ExtensibleArrayMaximumNumberOfElementsBits { get; }

        public byte IndexBlockElementsCount { get; }

        public byte DataBlockMininumElementsCount { get; }

        public byte SecondaryBlockMinimumDataBlockPointerCount { get; }

        public byte DataBlockPageMaximumNumberOfElementsBits { get; }

        public ulong SecondaryBlocksCount { get; }

        public ulong SecondaryBlocksSize { get; }

        public ulong DataBlocksCount { get; }

        public ulong DataBlocksSize { get; }

        public ulong MaximumIndexSet { get; }

        public ulong ElementsCount { get; }

        public ulong IndexBlockAddress { get; }

        public uint Checksum { get; }

        public ulong SecondaryBlockCount { get; private set; }

        public ulong DataBlockPageElementsCount { get; private set; }

        public byte ArrayOffsetsSize { get; private set; }

        public ExtensibleArraySecondaryBlockInformation[] SecondaryBlockInfos { get; private set; }

        #endregion

        #region Methods

        public uint ComputeSecondaryBlockIndex(ulong index)
        {
            // H5EAdblock.c (H5EA__dblock_sblk_idx)

            /* Adjust index for elements in index block */
            index -= this.IndexBlockElementsCount;

            /* Determine the superblock information for the index */
            var tmp = index / this.DataBlockMininumElementsCount;
            var secondaryBlockIndex = (uint)Math.Log(tmp + 1, 2);

            return secondaryBlockIndex;
        }

        private void Initialize()
        {
            // H5EA.hdr.c (H5EA__hdr_init)

            /* Compute general information */
            this.SecondaryBlockCount = 1UL +
                this.ExtensibleArrayMaximumNumberOfElementsBits -
                (uint)Math.Log(DataBlockMininumElementsCount, 2);

            this.DataBlockPageElementsCount = 1UL << this.DataBlockPageMaximumNumberOfElementsBits;
            this.ArrayOffsetsSize = (byte)((this.ExtensibleArrayMaximumNumberOfElementsBits + 7) / 8);

            /* Allocate information for each super block */
            this.SecondaryBlockInfos = new ExtensibleArraySecondaryBlockInformation[this.SecondaryBlockCount];

            /* Compute information about each super block */
            var elementStartIndex = 0UL;
            var dataBlockStartIndex = 0UL;

            for (ulong i = 0; i < this.SecondaryBlockCount; i++)
            {
                this.SecondaryBlockInfos[i].DataBlockCount = (ulong)(1 << ((int)i / 2));
                this.SecondaryBlockInfos[i].ElementsCount = (ulong)(1 << (((int)i + 1) / 2)) * this.DataBlockMininumElementsCount;
                this.SecondaryBlockInfos[i].ElementStartIndex = elementStartIndex;
                this.SecondaryBlockInfos[i].DataBlockStartIndex = dataBlockStartIndex;

                /* Advance starting indices for next super block */
                elementStartIndex += this.SecondaryBlockInfos[i].DataBlockCount * this.SecondaryBlockInfos[i].ElementsCount;
                dataBlockStartIndex += this.SecondaryBlockInfos[i].DataBlockCount;
            }
        }

        #endregion
    }
}
