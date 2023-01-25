using System.Runtime.InteropServices;

namespace PureHDF
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    partial struct H5ObjectReference
    {
        [FieldOffset(0)]
        internal ulong Value;
    }
}
