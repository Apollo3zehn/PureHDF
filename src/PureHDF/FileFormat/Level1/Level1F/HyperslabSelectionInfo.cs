namespace PureHDF
{
    internal abstract class HyperslabSelectionInfo
    {
        public uint Rank { get; set; }
        public ulong[] CompactDimensions { get; set; } = default!;
    }
}
