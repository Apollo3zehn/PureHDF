using System.Text;

namespace PureHDF
{
    internal class ExtensibleArraySecondaryBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ExtensibleArraySecondaryBlock(H5Context context, ExtensibleArrayHeader header, uint index)
        {
            var (driver, superblock) = context;

            // H5EAsblock.c (H5EA__sblock_alloc)

            /* Compute/cache information */
            var dataBlocksCount = header.SecondaryBlockInfos[index].DataBlockCount;
            ElementCount = header.SecondaryBlockInfos[index].ElementsCount;
            DataBlockPageCount = 0UL;
            var dataBlockPageInitBitMaskSize = 0UL;

            /* Check if # of elements in data blocks requires paging */
            if (ElementCount > header.DataBlockPageElementsCount)
            {
                /* Compute # of pages in each data block from this super block */
                DataBlockPageCount = ElementCount / header.DataBlockPageElementsCount;

                /* Sanity check that we have at least 2 pages in data block */
                if (DataBlockPageCount < 2)
                    throw new Exception("There must be at least two pages in the data block.");

                /* Compute size of buffer for each data block's 'page init' bitmask */
                dataBlockPageInitBitMaskSize = DataBlockPageCount + 7 / 8;

                /* Compute data block page size */
                DataBlockPageSize = header.DataBlockPageElementsCount * header.ElementSize + 4;
            }

            // signature
            var signature = driver.ReadBytes(4);
            Utils.ValidateSignature(signature, ExtensibleArraySecondaryBlock.Signature);

            // version
            Version = driver.ReadByte();

            // client ID
            ClientID = (ClientID)driver.ReadByte();

            // header address
            HeaderAddress = superblock.ReadOffset(driver);

            // block offset
            BlockOffset = Utils.ReadUlong(driver, header.ArrayOffsetsSize);

            // page bitmap
            // H5EAcache.c (H5EA__cache_sblock_deserialize)

            /* Check for 'page init' bitmasks for this super block */
            if (DataBlockPageCount > 0)
            {
                /* Compute total size of 'page init' buffer */
                var totalPageInitSize = dataBlocksCount * dataBlockPageInitBitMaskSize;

                /* Retrieve the 'page init' bitmasks */
                PageBitmap = driver.ReadBytes((int)totalPageInitSize);
            }
            else
            {
                PageBitmap = Array.Empty<byte>();
            }

            // data block addresses
            DataBlockAddresses = new ulong[dataBlocksCount];

            for (ulong i = 0; i < dataBlocksCount; i++)
            {
                DataBlockAddresses[i] = superblock.ReadOffset(driver);
            }

            // checksum
            Checksum = driver.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EASB");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArraySecondaryBlock)} are supported.");

                _version = value;
            }
        }

        public ClientID ClientID { get; }

        public ulong HeaderAddress { get; }

        public ulong BlockOffset { get; }

        public byte[] PageBitmap { get; }

        public ulong[] DataBlockAddresses { get; }

        public ulong Checksum { get; }

        public ulong ElementCount { get; }

        public ulong DataBlockPageCount { get; }

        public ulong DataBlockPageSize { get; }

        #endregion
    }
}
