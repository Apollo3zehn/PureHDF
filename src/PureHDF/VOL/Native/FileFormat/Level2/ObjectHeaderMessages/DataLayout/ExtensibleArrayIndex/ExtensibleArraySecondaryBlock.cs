using System.Text;

namespace PureHDF.VOL.Native;

internal record class ExtensibleArraySecondaryBlock(
    ClientID ClientID,
    ulong HeaderAddress,
    ulong BlockOffset,
    byte[] PageBitmap,
    ulong[] DataBlockAddresses,
    ulong ElementCount,
    ulong DataBlockPageCount,
    ulong DataBlockPageSize
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EASB");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArraySecondaryBlock)} are supported.");

            _version = value;
        }
    }

    public static ExtensibleArraySecondaryBlock Decode(NativeContext context, ExtensibleArrayHeader header, uint index)
    {
        var (driver, superblock) = context;

        // H5EAsblock.c (H5EA__sblock_alloc)

        /* Compute/cache information */
        var dataBlocksCount = header.SecondaryBlockInfos[index].DataBlockCount;
        var elementCount = header.SecondaryBlockInfos[index].ElementsCount;
        var dataBlockPageCount = 0UL;
        var dataBlockPageInitBitMaskSize = 0UL;

        /* Check if # of elements in data blocks requires paging */
        var dataBlockPageSize = default(ulong);

        if (elementCount > header.DataBlockPageElementsCount)
        {
            /* Compute # of pages in each data block from this super block */
            dataBlockPageCount = elementCount / header.DataBlockPageElementsCount;

            /* Sanity check that we have at least 2 pages in data block */
            if (dataBlockPageCount < 2)
                throw new Exception("There must be at least two pages in the data block.");

            /* Compute size of buffer for each data block's 'page init' bitmask */
            dataBlockPageInitBitMaskSize = dataBlockPageCount + 7 / 8;

            /* Compute data block page size */
            dataBlockPageSize = header.DataBlockPageElementsCount * header.ElementSize + 4;
        }

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // client ID
        var clientID = (ClientID)driver.ReadByte();

        // header address
        var headerAddress = superblock.ReadOffset(driver);

        // block offset
        var blockOffset = Utils.ReadUlong(driver, header.ArrayOffsetsSize);

        // page bitmap
        // H5EAcache.c (H5EA__cache_sblock_deserialize)

        /* Check for 'page init' bitmasks for this super block */
        byte[] pageBitmap;

        if (dataBlockPageCount > 0)
        {
            /* Compute total size of 'page init' buffer */
            var totalPageInitSize = dataBlocksCount * dataBlockPageInitBitMaskSize;

            /* Retrieve the 'page init' bitmasks */
            pageBitmap = driver.ReadBytes((int)totalPageInitSize);
        }

        else
        {
            pageBitmap = Array.Empty<byte>();
        }

        // data block addresses
        var dataBlockAddresses = new ulong[dataBlocksCount];

        for (ulong i = 0; i < dataBlocksCount; i++)
        {
            dataBlockAddresses[i] = superblock.ReadOffset(driver);
        }

        // checksum
        var _ = driver.ReadUInt32();

        return new ExtensibleArraySecondaryBlock(
            ClientID: clientID,
            HeaderAddress: headerAddress,
            BlockOffset: blockOffset,
            PageBitmap: pageBitmap,
            DataBlockAddresses: dataBlockAddresses,
            ElementCount: elementCount,
            DataBlockPageCount: dataBlockPageCount,
            DataBlockPageSize: dataBlockPageSize
        )
        {
            Version = version
        };
    }
}