using System.Runtime.InteropServices;

namespace PureHDF.Tests;

public struct TestStructStringAndArray
{
    public float FloatValue;

    [MarshalAs(UnmanagedType.LPStr)]
    public string StringValue1;

    [MarshalAs(UnmanagedType.LPStr)]
    public string StringValue2;

    public byte ByteValue;

    [H5Name("ShortValue")]
    public short ShortValueWithCustomName;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] /* only to correctly create reading test files */
    public float[] FloatArray;

    public TestStructL2 L2Struct;
}