using System.Text;

namespace PureHDF.VOL.Native;

internal class FixedArrayDataBlock<T>
{
    #region Fields

    private byte _version;

    #endregion

    #region Constructors

    public FixedArrayDataBlock(H5Context context, FixedArrayHeader header, Func<H5DriverBase, T> decode)
    {
        var (driver, superblock) = context;

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
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, FixedArrayDataBlock<T>.Signature);

        // version
        Version = driver.ReadByte();

        // client ID
        ClientID = (ClientID)driver.ReadByte();

        // header address
        HeaderAddress = superblock.ReadOffset(driver);

        // page bitmap
        if (PageCount > 0)
        {
            PageBitmap = driver.ReadBytes((int)pageBitmapSize);
            Elements = Array.Empty<T>();
        }
        // elements
        else
        {
            PageBitmap = Array.Empty<byte>();

            Elements = Enumerable
                .Range(0, (int)header.EntriesCount)
                .Select(i => decode(driver))
                .ToArray();
        }

        // checksum
        Checksum = driver.ReadUInt32();

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