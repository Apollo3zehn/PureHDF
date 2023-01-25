using System.Text;

namespace HDF5.NET
{
    internal class FixedArrayDataBlock<T>
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public FixedArrayDataBlock(H5Context context, FixedArrayHeader header, Func<H5BaseReader, T> decode)
        {
            var (reader, superblock) = context;

            // H5FAdblock.c (H5FA__dblock_alloc)
            ElementsPerPage = 1UL << header.PageBits;
            PageCount = 0UL;

            var pageBitmapSize = 0UL;

            if (header.EntriesCount > ElementsPerPage)
            {
                /* Compute number of pages */
                PageCount = (header.EntriesCount + ElementsPerPage - 1) / ElementsPerPage;

                /* Compute size of 'page init' flag array, in bytes */
                pageBitmapSize = (PageCount + 7) / 8;
            }

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FixedArrayDataBlock<T>.Signature);

            // version
            Version = reader.ReadByte();

            // client ID
            ClientID = (ClientID)reader.ReadByte();

            // header address
            HeaderAddress = superblock.ReadOffset(reader);

            // page bitmap
            if (PageCount > 0)
            {
                PageBitmap = reader.ReadBytes((int)pageBitmapSize);
                Elements = new T[0];
            }
            // elements
            else
            {
                PageBitmap = new byte[0];

                Elements = Enumerable
                    .Range(0, (int)header.EntriesCount)
                    .Select(i => decode(reader))
                    .ToArray();
            }

            // checksum
            Checksum = reader.ReadUInt32();

            // last page element count
            if (header.EntriesCount % ElementsPerPage == 0)
                LastPageElementCount = ElementsPerPage;

            else
                LastPageElementCount = header.EntriesCount % ElementsPerPage;
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
                    throw new FormatException($"Only version 0 instances of type {nameof(FixedArrayDataBlock<T>)} are supported.");

                _version = value;
            }
        }

        public ClientID ClientID { get; }

        public ulong HeaderAddress { get; }

        public byte[] PageBitmap { get; }

        public T[] Elements { get; }

        public ulong Checksum { get; }

        public ulong ElementsPerPage { get; }

        public ulong PageCount { get; }

        public ulong LastPageElementCount { get; }

        #endregion
    }
}
