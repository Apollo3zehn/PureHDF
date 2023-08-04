using System.Text;

namespace PureHDF.VOL.Native;

internal record class ExtensibleArrayDataBlock<T>(
    ClientID ClientID,
    ulong HeaderAddress,
    T[] Elements,
    ulong BlockOffset,
    ulong PageCount
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EADB");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArrayDataBlock<T>)} are supported.");

            _version = value;
        }
    }

    public static ExtensibleArrayDataBlock<T> Decode(
        NativeContext context, 
        ExtensibleArrayHeader header,
        ulong elementCount, Func<H5DriverBase, T> decode)
    {
        var (driver, superblock) = context;

        // H5EAdblock.c (H5EA__dblock_alloc)
        var pageCount = 0UL;

        if (elementCount > header.DataBlockPageElementsCount)
        {
            /* Set the # of pages in the data block */
            pageCount = elementCount / header.DataBlockPageElementsCount;
        }

        // H5EAcache.c (H5EA__cache_dblock_deserialize)

        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, ExtensibleArrayDataBlock<T>.Signature);

        // version
        var version = driver.ReadByte();

        // client ID
        var clientID = (ClientID)driver.ReadByte();

        // header address
        var headerAddress = superblock.ReadOffset(driver);

        // block offset
        var blockOffset = ReadUtils.ReadUlong(driver, header.ArrayOffsetsSize);

        // elements
        T[] elements;

        if (pageCount == 0)
        {
            elements = Enumerable
                .Range(0, (int)elementCount)
                .Select(i => decode(driver))
                .ToArray();
        }
        else
        {
            elements = Array.Empty<T>();
        }

        // checksum
        var _ = driver.ReadUInt32();

        return new ExtensibleArrayDataBlock<T>(
            ClientID: clientID,
            HeaderAddress: headerAddress,
            Elements: elements,
            BlockOffset: blockOffset,
            PageCount: pageCount
        )
        {
            Version = version
        };
    }
}