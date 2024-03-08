using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class FixedArrayDataBlock<T>(
    ClientID ClientID,
    ulong HeaderAddress,
    byte[] PageBitmap,
    T[] Elements,
    ulong ElementsPerPage,
    ulong PageCount,
    ulong LastPageElementCount
) where T : DataBlockElement
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
        ulong elementsPerPage,
        ulong pageCount,
        ulong pageBitmapSize,
        ulong entriesCount,
        Func<H5DriverBase, T> decode)
    {
        var (driver, superblock) = context;

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
                .Range(0, (int)entriesCount)
                .Select(i => decode(driver))
                .ToArray();
        }

        // checksum
        var _ = driver.ReadUInt32();

        // last page element count
        var lastPageElementCount = entriesCount % elementsPerPage == 0
            ? elementsPerPage
            : entriesCount % elementsPerPage;

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