using System;

namespace HDF5.NET
{
    [Flags]
    internal enum FractalHeapHeaderFlags : byte
    {
        IdValueIsWrapped = 1,
        DirectBlocksAreChecksummed = 2
    }
}
