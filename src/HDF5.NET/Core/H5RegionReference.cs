using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [StructLayout(LayoutKind.Explicit, Size = 12)]
    partial struct H5RegionReference
    {
        [FieldOffset(0)]
        internal ulong CollectionAddress;

        [FieldOffset(8)]
        internal uint ObjectIndex;
    }
}
