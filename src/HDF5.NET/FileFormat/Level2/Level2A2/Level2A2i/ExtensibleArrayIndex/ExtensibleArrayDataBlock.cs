using System;
using System.Text;

namespace HDF5.NET
{
    public class ExtensibleArrayDataBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ExtensibleArrayDataBlock(H5BinaryReader reader, Superblock superblock, ExtensibleArrayHeader header, uint chunkSizeLength, ulong elementsCount)
        {
            // H5EAdblock.c (H5EA__dblock_alloc)
            this.PageCount = 0UL;

            if (elementsCount > header.DataBlockPageElementsCount)
            {
                /* Set the # of pages in the data block */
                this.PageCount = elementsCount / header.DataBlockPageElementsCount;
            }

            // H5EAcache.c (H5EA__cache_dblock_deserialize)

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, ExtensibleArrayDataBlock.Signature);

            // version
            this.Version = reader.ReadByte();

            // client ID
            this.ClientID = (ClientID)reader.ReadByte();

            // header address
            this.HeaderAddress = superblock.ReadOffset(reader);

            // block offset
            this.BlockOffset = H5Utils.ReadUlong(reader, header.ArrayOffsetsSize);

            // elements
            if (this.PageCount == 0)
                this.Elements = ArrayIndexUtils.ReadElements(reader, superblock, elementsCount, this.ClientID, chunkSizeLength);

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EADB");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FixedArrayDataBlock)} are supported.");

                _version = value;
            }
        }

        public ClientID ClientID { get; }

        public ulong HeaderAddress { get; }

        public DataBlockElement[]? Elements { get; }

        public ulong Checksum { get; }

        public ulong BlockOffset { get; }

        public ulong PageCount { get; }

        #endregion
    }
}
