using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    partial struct H5ObjectReference
    {
        [FieldOffset(0)]
        internal ulong Value;
    }
}
