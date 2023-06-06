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

    public static ExtensibleArrayHeader Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // client ID
        var clientID = (ClientID)driver.ReadByte();

        // byte fields
        var elementSize = driver.ReadByte();
        var extensibleArrayMaximumNumberOfElementsBits = driver.ReadByte();
        var indexBlockElementsCount = driver.ReadByte();
        var dataBlockMininumElementsCount = driver.ReadByte();
        var secondaryBlockMinimumDataBlockPointerCount = driver.ReadByte();
        var dataBlockPageMaximumNumberOfElementsBits = driver.ReadByte();

        // length fields
        var secondaryBlocksCount = superblock.ReadLength(driver);
        var secondaryBlocksSize = superblock.ReadLength(driver);
        var dataBlocksCount = superblock.ReadLength(driver);
        var dataBlocksSize = superblock.ReadLength(driver);
        var maximumIndexSet = superblock.ReadLength(driver);
        var elementsCount = superblock.ReadLength(driver);

        // index block address
        var indexBlockAddress = superblock.ReadOffset(driver);

        // checksum
        var _ = driver.ReadUInt32();

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