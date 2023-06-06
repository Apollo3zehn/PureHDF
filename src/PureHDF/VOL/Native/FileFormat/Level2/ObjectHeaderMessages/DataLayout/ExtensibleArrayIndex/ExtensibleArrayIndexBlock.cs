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

    public static ExtensibleArrayIndexBlock<T> Decode(
        H5DriverBase driver,
        Superblock superblock,
        ExtensibleArrayHeader header,
        Func<H5DriverBase, T> decode)
    {
        // H5EAiblock.c (H5EA__iblock_alloc)
        var secondaryBlockDataBlockAddressCount = 2 * (ulong)Math.Log(header.SecondaryBlockMinimumDataBlockPointerCount, 2);
        ulong dataBlockPointerCount = (ulong)(2 * (header.SecondaryBlockMinimumDataBlockPointerCount - 1));
        ulong secondaryBlockPointerCount = header.SecondaryBlockCount - secondaryBlockDataBlockAddressCount;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, ExtensibleArrayIndexBlock<T>.Signature);

        // version
        var version = driver.ReadByte();

        // client ID
        var clientID = (ClientID)driver.ReadByte();

        // header address
        var headerAddress = superblock.ReadOffset(driver);

        // elements
        var elements = Enumerable
            .Range(0, header.IndexBlockElementsCount)
            .Select(i => decode(driver))
            .ToArray();

        // data block addresses
        var dataBlockAddresses = new ulong[dataBlockPointerCount];

        for (ulong i = 0; i < dataBlockPointerCount; i++)
        {
            dataBlockAddresses[i] = superblock.ReadOffset(driver);
        }

        // secondary block addresses
        var secondaryBlockAddresses = new ulong[secondaryBlockPointerCount];

        for (ulong i = 0; i < secondaryBlockPointerCount; i++)
        {
            secondaryBlockAddresses[i] = superblock.ReadOffset(driver);
        }

        // checksum
        var _ = driver.ReadUInt32();

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