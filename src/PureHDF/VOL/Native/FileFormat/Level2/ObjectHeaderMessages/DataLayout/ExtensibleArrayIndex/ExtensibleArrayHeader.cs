using System.Text;

namespace PureHDF.VOL.Native;

internal record class ExtensibleArrayHeader(
    ClientID ClientID,
    byte ElementSize,
    byte ExtensibleArrayMaximumNumberOfElementsBits,
    byte IndexBlockElementsCount,
    byte DataBlockMininumElementsCount,
    byte SecondaryBlockMinimumDataBlockPointerCount,
    byte DataBlockPageMaximumNumberOfElementsBits,
    ulong SecondaryBlocksCount,
    ulong SecondaryBlocksSize,
    ulong DataBlocksCount,
    ulong DataBlocksSize,
    ulong MaximumIndexSet,
    ulong ElementsCount,
    ulong IndexBlockAddress,
    ulong SecondaryBlockCount,
    ulong DataBlockPageElementsCount,
    byte ArrayOffsetsSize,
    ExtensibleArraySecondaryBlockInformation[] SecondaryBlockInfos
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EAHD");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArrayHeader)} are supported.");

            _version = value;
        }
    }

    public static async ValueTask<ExtensibleArrayHeader> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // client ID
        var clientID = (ClientID)await driver.ReadByte().ConfigureAwait(false);

        // byte fields
        var elementSize = await driver.ReadByte().ConfigureAwait(false);
        var extensibleArrayMaximumNumberOfElementsBits = await driver.ReadByte().ConfigureAwait(false);
        var indexBlockElementsCount = await driver.ReadByte().ConfigureAwait(false);
        var dataBlockMininumElementsCount = await driver.ReadByte().ConfigureAwait(false);
        var secondaryBlockMinimumDataBlockPointerCount = await driver.ReadByte().ConfigureAwait(false);
        var dataBlockPageMaximumNumberOfElementsBits = await driver.ReadByte().ConfigureAwait(false);

        // length fields
        var secondaryBlocksCount = await superblock.ReadLength(driver).ConfigureAwait(false);
        var secondaryBlocksSize = await superblock.ReadLength(driver).ConfigureAwait(false);
        var dataBlocksCount = await superblock.ReadLength(driver).ConfigureAwait(false);
        var dataBlocksSize = await superblock.ReadLength(driver).ConfigureAwait(false);
        var maximumIndexSet = await superblock.ReadLength(driver).ConfigureAwait(false);
        var elementsCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // index block address
        var indexBlockAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

        // H5EA.hdr.c (H5EA__hdr_init)

        /* Compute general information */
        var secondaryBlockCount = 1UL +
            extensibleArrayMaximumNumberOfElementsBits -
            (uint)Math.Log(dataBlockMininumElementsCount, 2);

        var dataBlockPageElementsCount = 1UL << dataBlockPageMaximumNumberOfElementsBits;
        var arrayOffsetsSize = (byte)((extensibleArrayMaximumNumberOfElementsBits + 7) / 8);

        /* Allocate information for each super block */
        var secondaryBlockInfos = new ExtensibleArraySecondaryBlockInformation[secondaryBlockCount];

        /* Compute information about each super block */
        var elementStartIndex = 0UL;
        var dataBlockStartIndex = 0UL;

        for (ulong i = 0; i < secondaryBlockCount; i++)
        {
            secondaryBlockInfos[i] = new ExtensibleArraySecondaryBlockInformation(
                DataBlockCount: (ulong)(1 << ((int)i / 2)),
                ElementsCount: (ulong)(1 << (((int)i + 1) / 2)) * dataBlockMininumElementsCount,
                ElementStartIndex: elementStartIndex,
                DataBlockStartIndex: dataBlockStartIndex
            );

            /* Advance starting indices for next super block */
            elementStartIndex += secondaryBlockInfos[i].DataBlockCount * secondaryBlockInfos[i].ElementsCount;
            dataBlockStartIndex += secondaryBlockInfos[i].DataBlockCount;
        }

        return new ExtensibleArrayHeader(
            ClientID: clientID,
            ElementSize: elementSize,
            ExtensibleArrayMaximumNumberOfElementsBits: extensibleArrayMaximumNumberOfElementsBits,
            IndexBlockElementsCount: indexBlockElementsCount,
            DataBlockMininumElementsCount: dataBlockMininumElementsCount,
            SecondaryBlockMinimumDataBlockPointerCount: secondaryBlockMinimumDataBlockPointerCount,
            DataBlockPageMaximumNumberOfElementsBits: dataBlockPageMaximumNumberOfElementsBits,
            SecondaryBlocksCount: secondaryBlocksCount,
            SecondaryBlocksSize: secondaryBlocksSize,
            DataBlocksCount: dataBlocksCount,
            DataBlocksSize: dataBlocksSize,
            MaximumIndexSet: maximumIndexSet,
            ElementsCount: elementsCount,
            IndexBlockAddress: indexBlockAddress,
            SecondaryBlockCount: secondaryBlockCount,
            DataBlockPageElementsCount: dataBlockPageElementsCount,
            ArrayOffsetsSize: arrayOffsetsSize,
            SecondaryBlockInfos: secondaryBlockInfos
        )
        {
            Version = version
        };
    }

    public uint ComputeSecondaryBlockIndex(ulong index)
    {
        // H5EAdblock.c (H5EA__dblock_sblk_idx)

        /* Adjust index for elements in index block */
        index -= IndexBlockElementsCount;

        /* Determine the superblock information for the index */
        var tmp = index / DataBlockMininumElementsCount;
        var secondaryBlockIndex = (uint)Math.Log(tmp + 1, 2);

        return secondaryBlockIndex;
    }
}