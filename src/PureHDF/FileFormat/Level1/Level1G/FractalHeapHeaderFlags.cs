namespace PureHDF
{
    [Flags]
    internal enum FractalHeapHeaderFlags : byte
    {
        IdValueIsWrapped = 1,
        DirectBlocksAreChecksummed = 2
    }
}
