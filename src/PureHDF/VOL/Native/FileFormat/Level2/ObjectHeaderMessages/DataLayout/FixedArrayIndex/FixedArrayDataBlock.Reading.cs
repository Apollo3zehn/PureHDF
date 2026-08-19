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

    public static async ValueTask<FixedArrayDataBlock<T>> Decode(
        NativeReadContext context,
        ulong elementsPerPage,
        ulong pageCount,
        ulong pageBitmapSize,
        ulong entriesCount,
        Func<H5DriverBase, ValueTask<T>> decode)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, FixedArrayDataBlock<T>.Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // client ID
        var clientID = (ClientID)await driver.ReadByte().ConfigureAwait(false);

        // header address
        var headerAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // page bitmap
        byte[] pageBitmap;
        T[] elements;

        if (pageCount > 0)
        {
            pageBitmap = await driver.ReadBytes((int)pageBitmapSize).ConfigureAwait(false);
            elements = Array.Empty<T>();
        }

        // elements
        else
        {
            pageBitmap = Array.Empty<byte>();

            elements = new T[(int)entriesCount];

            for (var i = 0; i < (int)entriesCount; i++)
            {
                elements[i] = await decode(driver).ConfigureAwait(false);
            }
        }

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

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