using System;
using System.Text;

namespace HDF5.NET
{
    public class FixedArrayDataBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public FixedArrayDataBlock(H5BinaryReader reader, Superblock superblock, FixedArrayHeader header, uint chunkSizeLength)
        {
            // H5FAdblock.c (H5FA__dblock_alloc)
            this.ElementsPerPage = 1UL << header.PageBits;
            this.PageCount = 0UL;

            var pageBitmapSize = 0UL;

            if (header.EntriesCount > this.ElementsPerPage)
            {
                /* Compute number of pages */
                this.PageCount = (header.EntriesCount + this.ElementsPerPage - 1) / this.ElementsPerPage;

                /* Compute size of 'page init' flag array, in bytes */
                pageBitmapSize = (this.PageCount + 7) / 8;
            }

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FixedArrayDataBlock.Signature);

            // version
            this.Version = reader.ReadByte();

            // client ID
            this.ClientID = (ClientID)reader.ReadByte();

            // header address
            this.HeaderAddress = superblock.ReadOffset(reader);

            // page bitmap
            if (this.PageCount > 0)
                this.PageBitmap = reader.ReadBytes((int)pageBitmapSize);

            // elements
            else
                this.Elements = ArrayIndexUtils.ReadElements(reader, superblock, header.EntriesCount, this.ClientID, chunkSizeLength);

            // checksum
            this.Checksum = reader.ReadUInt32();

            // last page element count
            if (header.EntriesCount % this.ElementsPerPage == 0)
                this.LastPageElementCount = this.ElementsPerPage;

            else
                this.LastPageElementCount = header.EntriesCount % this.ElementsPerPage;
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FADB");

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

        public byte[]? PageBitmap { get; }

        public DataBlockElement[]? Elements { get; }

        public ulong Checksum { get; }

        public ulong ElementsPerPage { get; }

        public ulong PageCount { get; }

        public ulong LastPageElementCount { get; }

        #endregion
    }
}
