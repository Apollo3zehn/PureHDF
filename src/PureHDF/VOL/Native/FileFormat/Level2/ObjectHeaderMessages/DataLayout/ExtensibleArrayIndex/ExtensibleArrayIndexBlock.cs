using System.Text;

namespace PureHDF.VOL.Native;

internal record class ExtensibleArrayIndexBlock<T>(
    ClientID ClientID,
    ulong HeaderAddress,
    T[] Elements,
    ulong[] DataBlockAddresses,
    ulong[] SecondaryBlockAddresses,
    ulong SecondaryBlockDataBlockAddressCount
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EAIB");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArrayIndexBlock<T>)} are supported.");

            _version = value;
        }
    }

    public static async ValueTask<ExtensibleArrayIndexBlock<T>> Decode(
        H5DriverBase driver,
        Superblock superblock,
        ExtensibleArrayHeader header,
        Func<H5DriverBase, ValueTask<T>> decode)
    {
        // H5EAiblock.c (H5EA__iblock_alloc)
        var secondaryBlockDataBlockAddressCount = 2 * (ulong)Math.Log(header.SecondaryBlockMinimumDataBlockPointerCount, 2);
        ulong dataBlockPointerCount = (ulong)(2 * (header.SecondaryBlockMinimumDataBlockPointerCount - 1));
        ulong secondaryBlockPointerCount = header.SecondaryBlockCount - secondaryBlockDataBlockAddressCount;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, ExtensibleArrayIndexBlock<T>.Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // client ID
        var clientID = (ClientID)await driver.ReadByte().ConfigureAwait(false);

        // header address
        var headerAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // elements
        var elements = new T[header.IndexBlockElementsCount];

        for (var i = 0; i < header.IndexBlockElementsCount; i++)
        {
            elements[i] = await decode(driver).ConfigureAwait(false);
        }

        // data block addresses
        var dataBlockAddresses = new ulong[dataBlockPointerCount];

        for (ulong i = 0; i < dataBlockPointerCount; i++)
        {
            dataBlockAddresses[i] = await superblock.ReadOffset(driver).ConfigureAwait(false);
        }

        // secondary block addresses
        var secondaryBlockAddresses = new ulong[secondaryBlockPointerCount];

        for (ulong i = 0; i < secondaryBlockPointerCount; i++)
        {
            secondaryBlockAddresses[i] = await superblock.ReadOffset(driver).ConfigureAwait(false);
        }

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

        return new ExtensibleArrayIndexBlock<T>(
            ClientID: clientID,
            HeaderAddress: headerAddress,
            Elements: elements,
            DataBlockAddresses: dataBlockAddresses,
            SecondaryBlockAddresses: secondaryBlockAddresses,
            SecondaryBlockDataBlockAddressCount: secondaryBlockDataBlockAddressCount
        )
        {
            Version = version
        };
    }
}