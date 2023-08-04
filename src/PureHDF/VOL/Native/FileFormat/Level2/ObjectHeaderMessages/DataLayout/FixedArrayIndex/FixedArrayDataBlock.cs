using System.Text;

namespace PureHDF.VOL.Native;

internal record class FixedArrayDataBlock<T>(
    ClientID ClientID,
    ulong HeaderAddress,
    byte[] PageBitmap,
    T[] Elements,
    ulong ElementsPerPage,
    ulong PageCount,
    ulong LastPageElementCount
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FADB");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FixedArrayDataBlock<T>)} are supported.");

            _version = value;
        }
    }

    public static FixedArrayDataBlock<T> Decode(
        NativeReadContext context,
        FixedArrayHeader header,
        Func<H5DriverBase, T> decode)
    {
        var (driver, superblock) = context;

        // H5FAdblock.c (H5FA__dblock_alloc)
        var elementsPerPage = 1UL << header.PageBits;
        var pageCount = 0UL;

        var pageBitmapSize = 0UL;

        if (header.EntriesCount > elementsPerPage)
        {
            /* Compute number of pages */
            pageCount = (header.EntriesCount + elementsPerPage - 1) / elementsPerPage;

            /* Compute size of 'page init' flag array, in bytes */
            pageBitmapSize = (pageCount + 7) / 8;
        }

        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, FixedArrayDataBlock<T>.Signature);

        // version
        var version = driver.ReadByte();

        // client ID
        var clientID = (ClientID)driver.ReadByte();

        // header address
        var headerAddress = superblock.ReadOffset(driver);

        // page bitmap
        byte[] pageBitmap;
        T[] elements;

        if (pageCount > 0)
        {
            pageBitmap = driver.ReadBytes((int)pageBitmapSize);
            elements = Array.Empty<T>();
        }

        // elements
        else
        {
            pageBitmap = Array.Empty<byte>();

            elements = Enumerable
                .Range(0, (int)header.EntriesCount)
                .Select(i => decode(driver))
                .ToArray();
        }

        // checksum
        var _ = driver.ReadUInt32();

        // last page element count
        var lastPageElementCount = header.EntriesCount % elementsPerPage == 0
            ? elementsPerPage
            : header.EntriesCount % elementsPerPage;

        return new FixedArrayDataBlock<T>(
            ClientID: clientID,
            HeaderAddress: headerAddress,
            PageBitmap: pageBitmap,
            Elements: elements,
            ElementsPerPage: elementsPerPage,
            PageCount: pageCount,
            LastPageElementCount: lastPageElementCount
        )
        {
            Version = version
        };
    }
}