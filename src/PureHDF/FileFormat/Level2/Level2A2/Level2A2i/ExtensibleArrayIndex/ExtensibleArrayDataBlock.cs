using System.Text;

namespace PureHDF
{
    internal class ExtensibleArrayDataBlock<T>
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ExtensibleArrayDataBlock(H5Context context, ExtensibleArrayHeader header, ulong elementCount, Func<H5BaseReader, T> decode)
        {
            var (reader, superblock) = context;

            // H5EAdblock.c (H5EA__dblock_alloc)
            PageCount = 0UL;

            if (elementCount > header.DataBlockPageElementsCount)
            {
                /* Set the # of pages in the data block */
                PageCount = elementCount / header.DataBlockPageElementsCount;
            }

            // H5EAcache.c (H5EA__cache_dblock_deserialize)

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, ExtensibleArrayDataBlock<T>.Signature);

            // version
            Version = reader.ReadByte();

            // client ID
            ClientID = (ClientID)reader.ReadByte();

            // header address
            HeaderAddress = superblock.ReadOffset(reader);

            // block offset
            BlockOffset = H5Utils.ReadUlong(reader, header.ArrayOffsetsSize);

            // elements
            if (PageCount == 0)
            {
                Elements = Enumerable
                    .Range(0, (int)elementCount)
                    .Select(i => decode(reader))
                    .ToArray();
            }
            else
            {
                Elements = Array.Empty<T>();
            }

            // checksum
            Checksum = reader.ReadUInt32();
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
                    throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArrayDataBlock<T>)} are supported.");

                _version = value;
            }
        }

        public ClientID ClientID { get; }

        public ulong HeaderAddress { get; }

        public T[] Elements { get; }

        public ulong Checksum { get; }

        public ulong BlockOffset { get; }

        public ulong PageCount { get; }

        #endregion
    }
}
