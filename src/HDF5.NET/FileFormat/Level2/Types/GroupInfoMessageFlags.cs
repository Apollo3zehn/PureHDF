using System;

namespace HDF5.NET
{
    [Flags]
    public enum GroupInfoMessageFlags : byte
    {
        StoreLinkPhaseChangeValues = 1,
        StoreNonDefaultEntryInformation = 2
    }
}
