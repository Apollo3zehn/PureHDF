using HDF.PInvoke;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddHuge(long fileId, ContainerType container, H5F.libver_t version)
        {
            var length = version switch
            {
                H5F.libver_t.EARLIEST => 16368UL, // max 64 kb in object header
                _ => (ulong)TestData.HugeData.Length,
            };

            Add(container, fileId, "huge", "huge", H5T.NATIVE_INT32, TestData.HugeData.AsSpan(), length);
        }

        public static unsafe void AddTiny(long fileId, ContainerType container)
        {
            Add(container, fileId, "tiny", "tiny", H5T.NATIVE_UINT8, TestData.TinyData.AsSpan());
        }
    }
}