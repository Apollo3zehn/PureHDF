﻿using System.Runtime.InteropServices;

namespace PureHDF.Tests;

public class TestObjectStringAndArray
{
    /* fields and properties are mixed to test both */

    public float FloatValue { get; set; }

    [MarshalAs(UnmanagedType.LPStr)]
    public string StringValue1 = default!;

    [MarshalAs(UnmanagedType.LPStr)]
    public string StringValue2 = default!;

    public byte ByteValue { get; set; }

    [H5Name("ShortValue")]
    public short ShortValueWithCustomName { get; set; }

    public float[] FloatArray = default!;

    public TestStructL2 L2Struct { get; set; }
}