using HDF.PInvoke;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddDataspaceScalar(long fileId, ContainerType container)
        {
            var spaceId = H5S.create(H5S.class_t.SCALAR);
            var data = new double[] { -1.2234234e-3 };

            fixed (void* dataPtr = data)
            {
                Add(container, fileId, "dataspace", "scalar", H5T.NATIVE_DOUBLE, dataPtr, spaceId);
            }

            _ = H5S.close(spaceId);
        }

        public static unsafe void AddDataspaceNull(long fileId, ContainerType container)
        {
            var spaceId = H5S.create(H5S.class_t.NULL);
            Add(container, fileId, "dataspace", "null", H5T.NATIVE_DOUBLE, null, spaceId);
            _ = H5S.close(spaceId);
        }
    }
}