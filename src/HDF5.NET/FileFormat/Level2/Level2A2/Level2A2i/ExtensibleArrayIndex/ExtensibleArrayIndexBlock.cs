using System;
using System.Linq;
using System.Text;

namespace HDF5.NET
{
    public class ExtensibleArrayIndexBlock<T>
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ExtensibleArrayIndexBlock(
            H5BinaryReader reader, 
            Superblock superblock, 
            ExtensibleArrayHeader header, 
            Func<H5BinaryReader, T> decode)
        {
            // H5EAiblock.c (H5EA__iblock_alloc)
            this.SecondaryBlockDataBlockAddressCount = 2 * (ulong)Math.Log(header.SecondaryBlockMinimumDataBlockPointerCount, 2);
            ulong dataBlockPointerCount = (ulong)(2 * (header.SecondaryBlockMinimumDataBlockPointerCount - 1));
            ulong secondaryBlockPointerCount = header.SecondaryBlockCount - this.SecondaryBlockDataBlockAddressCount;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, ExtensibleArrayIndexBlock<T>.Signature);

            // version
            this.Version = reader.ReadByte();

            // client ID
            this.ClientID = (ClientID)reader.ReadByte();

            // header address
            this.HeaderAddress = superblock.ReadOffset(reader);

            // elements
            this.Elements = Enumerable
                .Range(0, header.IndexBlockElementsCount)
                .Select(i => decode(reader))
                .ToArray();

            // data block addresses
            this.DataBlockAddresses = new ulong[dataBlockPointerCount];

            for (ulong i = 0; i < dataBlockPointerCount; i++)
            {
                this.DataBlockAddresses[i] = superblock.ReadOffset(reader);
            }

            // secondary block addresses
            this.SecondaryBlockAddresses = new ulong[secondaryBlockPointerCount];

            for (ulong i = 0; i < secondaryBlockPointerCount; i++)
            {
                this.SecondaryBlockAddresses[i] = superblock.ReadOffset(reader);
            }

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EAIB");

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

        public T[] Elements { get; }

        public ulong[] DataBlockAddresses { get; }

        public ulong[] SecondaryBlockAddresses { get; }

        public ulong Checksum { get; }

        public ulong SecondaryBlockDataBlockAddressCount { get; }

        #endregion
    }
}
