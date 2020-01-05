using System;

namespace HDF5.NET
{
    [Flags]
    public enum HeaderMessageFlags : ushort
    {
        Constant = 1,
        Shared = 2,
        DoNotShare = 4,
        FailOnUnknownTypeWithWritePermissions = 8,
        SetBit5OnUnknownType = 16,
        ModifiedAlthoughUnknownType = 32,
        Shareable = 64,
        FailOnUnknownTypeAlways = 128
    }
}
