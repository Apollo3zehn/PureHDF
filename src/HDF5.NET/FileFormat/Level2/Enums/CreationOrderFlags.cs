using System;

namespace HDF5.NET
{
    [Flags]
    public enum CreationOrderFlags : byte
    {
        TrackCreationOrder = 1,
        IndexCreationOrder = 2
    }
}
