using System.Runtime.InteropServices;

namespace PureHDF.Tests;

public class TestObjectStringAndArray
{
    /* fields and properties are mixed to test both */

    public float FloatValue { get; set; }

    [MarshalAs(UnmanagedType.LPStr)]
    public string StringValue1;

    [MarshalAs(UnmanagedType.LPStr)]
    public string StringValue2;

    public byte ByteValue { get; set; }

    [H5Name("ShortValue")]
    public short ShortValueWithCustomName { get; set; }

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] FloatArray;

    public TestStructL2 L2Struct { get; set; }
}