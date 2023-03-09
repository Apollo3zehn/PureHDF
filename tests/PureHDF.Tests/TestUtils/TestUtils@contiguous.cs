using System.Runtime.InteropServices;
using HDF.PInvoke;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddContiguousDataset(long fileId)
        {
            Add(ContainerType.Dataset, fileId, "contiguous", "contiguous", H5T.NATIVE_INT32, TestData.HugeData.AsSpan());
        }

        public static unsafe void AddContiguousDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            var length = (ulong)TestData.MediumData.Length;
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            _ = H5P.set_layout(dcpl_id, H5D.layout_t.CONTIGUOUS);

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            _ = H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            Add(ContainerType.Dataset, fileId, "fillvalue", $"{LayoutClass.Contiguous}", H5T.NATIVE_INT32, (void*)0, length, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddExternalDataset(long fileId, string datasetName)
        {
            var bytesoftype = 4;
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_layout(dcpl_id, H5D.layout_t.CONTIGUOUS);

            // a (more than one chunk in file)
            var pathA = $"{datasetName}_a.raw";

            if (File.Exists(pathA))
                File.Delete(pathA);

            _ = H5P.set_external(dcpl_id, pathA, new IntPtr(120), (ulong)(10 * bytesoftype));
            _ = H5P.set_external(dcpl_id, pathA, new IntPtr(80), (ulong)(10 * bytesoftype));
            _ = H5P.set_external(dcpl_id, pathA, new IntPtr(0), (ulong)(10 * bytesoftype));

            // b (file size smaller than set size)
            var pathB = $"{datasetName}_b.raw";

            if (File.Exists(pathB))
                File.Delete(pathB);

            _ = H5P.set_external(dcpl_id, pathB, new IntPtr(0), (ulong)(10 * bytesoftype));

            // c (normal file)
            var pathC = $"{datasetName}_c.raw";

            if (File.Exists(pathC))
                File.Delete(pathC);

            _ = H5P.set_external(dcpl_id, pathC, new IntPtr(0), (ulong)((TestData.MediumData.Length - 40) * bytesoftype));

            // write data
            Add(ContainerType.Dataset, fileId, "external", datasetName, H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), cpl: dcpl_id);

            // truncate file b
            using (var fileStream2 = File.OpenWrite(pathB))
            {
                fileStream2.SetLength(10);
            };

            _ = H5P.close(dcpl_id);
        }
    }
}