using System.Runtime.InteropServices;

namespace HDF5.NET.Tests
{
    public struct TestStructString
    {
        public float FloatValue;

        [MarshalAs(UnmanagedType.LPStr)]
        public string StringValue1;

        [MarshalAs(UnmanagedType.LPStr)]
        public string StringValue2;

        public byte ByteValue;

        [H5Name("ShortValue")]
        public short ShortValueWithCustomName;

        public TestStructL2 L2Struct;
    }
}
