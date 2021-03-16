using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public partial struct H5ObjectReference
    {
        [FieldOffset(0)]
        internal ulong Value;
    }
}
