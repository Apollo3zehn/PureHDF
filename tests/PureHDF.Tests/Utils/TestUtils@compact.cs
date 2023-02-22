using HDF.PInvoke;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddCompactDataset(long fileId)
        {
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_layout(dcpl_id, H5D.layout_t.COMPACT);
            Add(ContainerType.Dataset, fileId, "compact", "compact", H5T.NATIVE_INT32, TestData.SmallData.AsSpan(), cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }
    }
}