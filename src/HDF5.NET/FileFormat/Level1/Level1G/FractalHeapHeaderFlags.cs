using System;

namespace HDF5.NET
{
    [Flags]
    public enum FractalHeapHeaderFlags : byte
    {
        IdValueIsWrapped = 1,
        DirectBlocksAreChecksummed = 2
    }
}
