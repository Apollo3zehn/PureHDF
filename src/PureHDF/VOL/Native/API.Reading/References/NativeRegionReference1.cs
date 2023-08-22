using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

/// <summary>
/// An HDF5 region reference.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 12)]
public partial struct NativeRegionReference1
{
    [FieldOffset(0)]
    internal ulong CollectionAddress;

    [FieldOffset(8)]
    internal uint ObjectIndex;
}