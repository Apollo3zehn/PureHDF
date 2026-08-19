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

    public static async ValueTask<ExtensibleArrayDataBlock<T>> Decode(
        NativeReadContext context,
        ExtensibleArrayHeader header,
        ulong elementCount, Func<H5DriverBase, ValueTask<T>> decode)
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
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, ExtensibleArrayDataBlock<T>.Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // client ID
        var clientID = (ClientID)await driver.ReadByte().ConfigureAwait(false);

        // header address
        var headerAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // block offset
        var blockOffset = await ReadUtils.ReadUlong(driver, header.ArrayOffsetsSize).ConfigureAwait(false);

        // elements
        T[] elements;

        if (pageCount == 0)
        {
            elements = new T[(int)elementCount];

            for (var i = 0; i < (int)elementCount; i++)
            {
                elements[i] = await decode(driver).ConfigureAwait(false);
            }
        }
        else
        {
            elements = Array.Empty<T>();
        }

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

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