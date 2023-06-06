namespace PureHDF.VOL.Native;

// H5EApkg.h (H5EA_sblk_info_t)
internal readonly record struct ExtensibleArraySecondaryBlockInformation(
    ulong DataBlockCount,
    ulong ElementsCount,
    ulong ElementStartIndex,
    ulong DataBlockStartIndex
);