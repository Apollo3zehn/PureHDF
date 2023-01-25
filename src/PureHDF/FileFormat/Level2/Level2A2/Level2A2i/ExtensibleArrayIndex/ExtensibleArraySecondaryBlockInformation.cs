namespace PureHDF
{
    internal struct ExtensibleArraySecondaryBlockInformation
    {
        // H5EApkg.h (H5EA_sblk_info_t)
        #region Properties

        public ulong DataBlockCount { get; set; }

        public ulong ElementsCount { get; set; }

        public ulong ElementStartIndex { get; set; }

        public ulong DataBlockStartIndex { get; set; }

        #endregion
    }
}
