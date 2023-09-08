using System.Runtime.InteropServices;

namespace PureHDF.Tests;

[StructLayout(LayoutKind.Explicit, Size = 5)]
public unsafe struct TestStructL2
{
    [FieldOffset(0)]
    public byte ByteValue;

    [FieldOffset(1)]
    public ushort UShortValue;

    [FieldOffset(3)]
    public TestEnum EnumValue;
}