using System;

namespace HDF5.NET
{
    [Flags]
    internal enum MessageFlags : ushort
    {
        NoFlags = 0,
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
