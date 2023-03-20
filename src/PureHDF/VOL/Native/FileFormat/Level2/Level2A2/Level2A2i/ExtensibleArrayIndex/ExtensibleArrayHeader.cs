﻿using System.Text;

namespace PureHDF.VOL.Native;

internal class ExtensibleArrayHeader
{
    #region Fields

    private byte _version;

    #endregion

    #region Constructors

    public ExtensibleArrayHeader(H5Context context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, ExtensibleArrayHeader.Signature);

        // version
        Version = driver.ReadByte();

        // client ID
        ClientID = (ClientID)driver.ReadByte();

        // byte fields
        ElementSize = driver.ReadByte();
        ExtensibleArrayMaximumNumberOfElementsBits = driver.ReadByte();
        IndexBlockElementsCount = driver.ReadByte();
        DataBlockMininumElementsCount = driver.ReadByte();
        SecondaryBlockMinimumDataBlockPointerCount = driver.ReadByte();
        DataBlockPageMaximumNumberOfElementsBits = driver.ReadByte();

        // length fields
        SecondaryBlocksCount = superblock.ReadLength(driver);
        SecondaryBlocksSize = superblock.ReadLength(driver);
        DataBlocksCount = superblock.ReadLength(driver);
        DataBlocksSize = superblock.ReadLength(driver);
        MaximumIndexSet = superblock.ReadLength(driver);
        ElementsCount = superblock.ReadLength(driver);

        // index block address
        IndexBlockAddress = superblock.ReadOffset(driver);

        // checksum
        Checksum = driver.ReadUInt32();

        // initialize
        Initialize();
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("EAHD");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(ExtensibleArrayHeader)} are supported.");

            _version = value;
        }
    }

    public ClientID ClientID { get; }

    public byte ElementSize { get; }

    public byte ExtensibleArrayMaximumNumberOfElementsBits { get; }

    public byte IndexBlockElementsCount { get; }

    public byte DataBlockMininumElementsCount { get; }

    public byte SecondaryBlockMinimumDataBlockPointerCount { get; }

    public byte DataBlockPageMaximumNumberOfElementsBits { get; }

    public ulong SecondaryBlocksCount { get; }

    public ulong SecondaryBlocksSize { get; }

    public ulong DataBlocksCount { get; }

    public ulong DataBlocksSize { get; }

    public ulong MaximumIndexSet { get; }

    public ulong ElementsCount { get; }

    public ulong IndexBlockAddress { get; }

    public uint Checksum { get; }

    public ulong SecondaryBlockCount { get; private set; }

    public ulong DataBlockPageElementsCount { get; private set; }

    public byte ArrayOffsetsSize { get; private set; }

    // Initialized in Initialize()
    public ExtensibleArraySecondaryBlockInformation[] SecondaryBlockInfos { get; private set; } = default!;

    #endregion

    #region Methods

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

    private void Initialize()
    {
        // H5EA.hdr.c (H5EA__hdr_init)

        /* Compute general information */
        SecondaryBlockCount = 1UL +
            ExtensibleArrayMaximumNumberOfElementsBits -
            (uint)Math.Log(DataBlockMininumElementsCount, 2);

        DataBlockPageElementsCount = 1UL << DataBlockPageMaximumNumberOfElementsBits;
        ArrayOffsetsSize = (byte)((ExtensibleArrayMaximumNumberOfElementsBits + 7) / 8);

        /* Allocate information for each super block */
        SecondaryBlockInfos = new ExtensibleArraySecondaryBlockInformation[SecondaryBlockCount];

        /* Compute information about each super block */
        var elementStartIndex = 0UL;
        var dataBlockStartIndex = 0UL;

        for (ulong i = 0; i < SecondaryBlockCount; i++)
        {
            SecondaryBlockInfos[i].DataBlockCount = (ulong)(1 << ((int)i / 2));
            SecondaryBlockInfos[i].ElementsCount = (ulong)(1 << (((int)i + 1) / 2)) * DataBlockMininumElementsCount;
            SecondaryBlockInfos[i].ElementStartIndex = elementStartIndex;
            SecondaryBlockInfos[i].DataBlockStartIndex = dataBlockStartIndex;

            /* Advance starting indices for next super block */
            elementStartIndex += SecondaryBlockInfos[i].DataBlockCount * SecondaryBlockInfos[i].ElementsCount;
            dataBlockStartIndex += SecondaryBlockInfos[i].DataBlockCount;
        }
    }

    #endregion
}