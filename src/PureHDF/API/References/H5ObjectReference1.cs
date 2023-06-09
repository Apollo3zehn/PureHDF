using System.Runtime.InteropServices;

namespace PureHDF;

/// <summary>
/// An HDF5 object reference.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct H5ObjectReference
{
    [FieldOffset(0)]
    internal ulong Value;
}