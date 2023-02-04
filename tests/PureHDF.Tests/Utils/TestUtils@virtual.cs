using HDF.PInvoke;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddVirtualDataset_2D(long fileId, string datasetName, string absolutePrefix, H5DatasetAccess datasetAccess)
        {
            // see VirtualMapping.xlsx for a visualization

            var vspaceId = H5S.create_simple(1, new ulong[] { 20 }, new ulong[] { 20 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            unsafe
            {
                var value = -1;
                _ = H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT32, new IntPtr(&value));
            }

            // file 1
            var fileName1 = $"{datasetName}_a.h5";
            var path1 = FilePathUtils.FindExternalFileForDatasetAccess(Path.Combine(absolutePrefix, fileName1), datasetAccess);

            if (File.Exists(path1))
                File.Delete(path1);

            var fileId1 = H5F.create(path1, H5F.ACC_TRUNC);
            var dataA = TestData.SmallData.Skip(0).Take(16).ToArray();
            Add(ContainerType.Dataset, fileId1, "vds", "source_a", H5T.NATIVE_INT32, dataA.AsSpan());

            var sourceSpaceId1 = H5S.create_simple(1, new ulong[] { (ulong)dataA.Length }, new ulong[] { (ulong)dataA.Length });

            _ = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, new ulong[] { 3 }, new ulong[] { 5 }, new ulong[] { 2 }, new ulong[] { 3 });
            _ = H5S.select_hyperslab(sourceSpaceId1, H5S.seloper_t.SET, new ulong[] { 2 }, new ulong[] { 5 }, new ulong[] { 3 }, new ulong[] { 2 });
            _ = H5P.set_virtual(dcpl_id, vspaceId, fileName1, "/vds/source_a", sourceSpaceId1);

            // file 2
            var fileName2 = $"{datasetName}_b.h5";
            var path2 = FilePathUtils.FindExternalFileForDatasetAccess(Path.Combine(absolutePrefix, fileName2), datasetAccess);

            if (File.Exists(path2))
                File.Delete(path2);

            var fileId2 = H5F.create(path2, H5F.ACC_TRUNC);
            var dataB = TestData.SmallData.Skip(10).Take(16).ToArray();
            Add(ContainerType.Dataset, fileId2, "vds", "source_b", H5T.NATIVE_INT32, dataB.AsSpan());

            var sourceSpaceId2 = H5S.create_simple(1, new ulong[] { (ulong)dataB.Length }, new ulong[] { (ulong)dataB.Length });

            _ = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, new ulong[] { 6 }, new ulong[] { 5 }, new ulong[] { 2 }, new ulong[] { 2 });
            _ = H5S.select_hyperslab(sourceSpaceId2, H5S.seloper_t.SET, new ulong[] { 3 }, new ulong[] { 4 }, new ulong[] { 4 }, new ulong[] { 1 });
            _ = H5P.set_virtual(dcpl_id, vspaceId, fileName2, "/vds/source_b", sourceSpaceId2);

            // file 3 (non-existent)
            var fileName3 = $"{datasetName}_c.h5";
            var sourceSpaceId3 = H5S.create_simple(1, new ulong[] { 10UL }, new ulong[] { 10UL });

            _ = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, new ulong[] { 15 }, new ulong[] { 1 }, new ulong[] { 1 }, new ulong[] { 1 });
            _ = H5S.select_hyperslab(sourceSpaceId3, H5S.seloper_t.SET, new ulong[] { 0 }, new ulong[] { 1 }, new ulong[] { 1 }, new ulong[] { 1 });
            _ = H5P.set_virtual(dcpl_id, vspaceId, fileName3, "/vds/source_c", sourceSpaceId3);

            // create virtual dataset
            var datasetId = H5D.create(fileId, "vds", H5T.NATIVE_INT32, vspaceId, dcpl_id: dcpl_id);

            _ = H5S.close(sourceSpaceId1);
            _ = H5F.close(fileId1);

            _ = H5S.close(sourceSpaceId2);
            _ = H5F.close(fileId2);

            _ = H5S.close(sourceSpaceId3);

            _ = H5S.close(vspaceId);
            _ = H5D.close(datasetId);

            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddVirtualDataset_source_irregular(long fileId, string datasetName)
        {
            /*
             *      0   1   2   3   4   5   6   7   8   9 
             *  00  -   -   -   -   -   -   -   -   -   - 
             *  10  -   -   -   B   B   B   -   -   -   - 
             *  20  -   -   -   B   B   B   -   -   -   - 
             *  30  -   -   -   B   B   B   -   -   -   - 
             *  40  -   -   -   -   -   -   -   -   -   - 
             *  50  -   -   -   B   B   B   -   -   -   - 
             *  60  -   A   A   A/B B  A/B  A   A   -   - 
             *  70  -   A   A   A/B B  A/B  A   A   -   - 
             *  80  -   A   A   A   -   A   A   A   -   - 
             *  90  -   -   -   B   B   B   -   -   -   - 
             * 100  -   -   -   B   B   B   -   -   -   - 
             * 110  -   -   -   B   B   B   -   -   -   - 
             * 120  -   -   -   -   -   -   -   -   -   - 
             *
             * A: selection A
             * B: selection B
             */

            // source file
            var sourceFileName = $"{datasetName}.h5";

            if (File.Exists(sourceFileName))
                File.Delete(sourceFileName);

            var sourceFileId = H5F.create(sourceFileName, H5F.ACC_TRUNC);

            // source dataset
            var sourceSpaceId = H5S.create_simple(2, new ulong[] { 13, 10 }, new ulong[] { 13, 10 });
            var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
            var memorySpaceId = H5S.create_simple(1, new ulong[] { 130 }, new ulong[] { 130 });
            var data = TestData.MediumData.Skip(0).Take(130).ToArray();

            unsafe
            {
                fixed (int* ptr = data.AsSpan())
                {
                    _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
                }
            }

            // source selection A
            _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.SET, 
                start: new ulong[] { 6, 1 }, 
                stride: new ulong[] { 3, 4 }, 
                count: new ulong[] { 1, 2 }, 
                block: new ulong[] { 3, 3 });

            // source selection B
            _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, 
                start: new ulong[] { 1, 3 }, 
                stride:new ulong[] { 4, 3 }, 
                count: new ulong[] { 3, 1 }, 
                block: new ulong[] { 3, 3 });

            // virtual dataset
            var virtualSpaceId = H5S.create_simple(1, new ulong[] { 41 }, new ulong[] { 41 });

            // create virtual dataset
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            _ = H5P.set_virtual(dcpl_id, virtualSpaceId, sourceFileName, "/source", sourceSpaceId);

            var datasetId = H5D.create(fileId, "vds", H5T.NATIVE_INT32, virtualSpaceId, dcpl_id: dcpl_id);

            // clean up
            _ = H5S.close(memorySpaceId);
            _ = H5S.close(sourceSpaceId);
            _ = H5D.close(sourceDatasetId);
            _ = H5F.close(sourceFileId);

            _ = H5S.close(virtualSpaceId);
            _ = H5D.close(datasetId);

            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddVirtualDataset_source_regular(long fileId, string datasetName)
        {
            /*
             *      0   1   2   3   4   5   6   7   8   9 
             *  00  -   -   -   -   -   -   -   -   -   - 
             *  10  -   -   -   A   A   A   -   -   -   - 
             *  20  -   -   -   A   A   A   -   -   -   - 
             *  30  -   -   -   A   A   A   -   -   -   - 
             *  40  -   -   -   -   -   -   -   -   -   - 
             *  50  -   -   -   A   A   A   -   -   -   - 
             * ...
             * 360  -   -   -   -   -   -   -   -   -   - 
             * 370  -   -   -   A   A   A   -   -   -   -
             * 380  -   -   -   A   A   A   -   -   -   -
             * 390  -   -   -   A   A   A   -   -   -   -
             *
             * A: selection A
             */

            // source file
            var sourceFileName = $"{datasetName}.h5";

            if (File.Exists(sourceFileName))
                File.Delete(sourceFileName);

            var sourceFileId = H5F.create(sourceFileName, H5F.ACC_TRUNC);

            // source dataset
            var sourceSpaceId = H5S.create_simple(2, new ulong[] { 40, 10 }, new ulong[] { 40, 10 });
            var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
            var memorySpaceId = H5S.create_simple(1, new ulong[] { 400 }, new ulong[] { 400 });
            var data = TestData.MediumData.Skip(0).Take(4000).ToArray();

            unsafe
            {
                fixed (int* ptr = data.AsSpan())
                {
                    _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
                }
            }

            // source selection A
            _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.SET, 
                start: new ulong[] { 1, 3 }, 
                stride: new ulong[] { 4, 3 }, 
                count: new ulong[] { 10, 1 }, 
                block: new ulong[] { 3, 3 });

            // virtual dataset
            var virtualSpaceId = H5S.create_simple(1, new ulong[] { 90 }, new ulong[] { 90 });

            // create virtual dataset
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            _ = H5P.set_virtual(dcpl_id, virtualSpaceId, sourceFileName, "/source", sourceSpaceId);

            var datasetId = H5D.create(fileId, "vds", H5T.NATIVE_INT32, virtualSpaceId, dcpl_id: dcpl_id);

            // clean up
            _ = H5S.close(memorySpaceId);
            _ = H5S.close(sourceSpaceId);
            _ = H5D.close(sourceDatasetId);
            _ = H5F.close(sourceFileId);

            _ = H5S.close(virtualSpaceId);
            _ = H5D.close(datasetId);

            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddVirtualDataset_virtual_irregular(long fileId, string datasetName)
        {
            /*
             *      0   1   2   3   4   5   6   7   8   9 
             *  00  -   -   -   -   -   -   -   -   -   - 
             *  10  -   -   -   B   B   B   -   -   -   - 
             *  20  -   -   -   B   B   B   -   -   -   - 
             *  30  -   -   -   B   B   B   -   -   -   - 
             *  40  -   -   -   -   -   -   -   -   -   - 
             *  50  -   -   -   B   B   B   -   -   -   - 
             *  60  -   A   A   A/B B  A/B  A   A   -   - 
             *  70  -   A   A   A/B B  A/B  A   A   -   - 
             *  80  -   A   A   A   -   A   A   A   -   - 
             *  90  -   -   -   B   B   B   -   -   -   - 
             * 100  -   -   -   B   B   B   -   -   -   - 
             * 110  -   -   -   B   B   B   -   -   -   - 
             * 120  -   -   -   -   -   -   -   -   -   - 
             *
             * A: selection A
             * B: selection B
             */

            // source file
            var sourceFileName = $"{datasetName}.h5";

            if (File.Exists(sourceFileName))
                File.Delete(sourceFileName);

            var sourceFileId = H5F.create(sourceFileName, H5F.ACC_TRUNC);

            // source dataset
            var sourceSpaceId = H5S.create_simple(1, new ulong[] { 41 }, new ulong[] { 41 });
            var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
            var memorySpaceId = H5S.create_simple(1, new ulong[] { 41 }, new ulong[] { 41 });
            var data = TestData.MediumData.Skip(0).Take(41).ToArray();

            unsafe
            {
                fixed (int* ptr = data.AsSpan())
                {
                    _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
                }
            }

            // virtual dataset
            var virtualSpaceId = H5S.create_simple(2, new ulong[] { 13, 10 }, new ulong[] { 13, 10 });

            // virtual selection A
            _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.SET, 
                start: new ulong[] { 6, 1 }, 
                stride: new ulong[] { 3, 4 }, 
                count: new ulong[] { 1, 2 }, 
                block: new ulong[] { 3, 3 });

            // virtual selection B
            _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, 
                start: new ulong[] { 1, 3 }, 
                stride:new ulong[] { 4, 3 }, 
                count: new ulong[] { 3, 1 }, 
                block: new ulong[] { 3, 3 });

            // create virtual dataset
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            _ = H5P.set_virtual(dcpl_id, virtualSpaceId, sourceFileName, "/source", sourceSpaceId);

            var datasetId = H5D.create(fileId, "vds", H5T.NATIVE_INT32, virtualSpaceId, dcpl_id: dcpl_id);

            // clean up
            _ = H5S.close(memorySpaceId);
            _ = H5S.close(sourceSpaceId);
            _ = H5D.close(sourceDatasetId);
            _ = H5F.close(sourceFileId);

            _ = H5S.close(virtualSpaceId);
            _ = H5D.close(datasetId);

            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddVirtualDataset_virtual_regular(long fileId, string datasetName)
        {
            /*
             *      0   1   2   3   4   5   6   7   8   9 
             *  00  -   -   -   -   -   -   -   -   -   - 
             *  10  -   -   -   A   A   A   -   -   -   - 
             *  20  -   -   -   A   A   A   -   -   -   - 
             *  30  -   -   -   A   A   A   -   -   -   - 
             *  40  -   -   -   -   -   -   -   -   -   - 
             *  50  -   -   -   A   A   A   -   -   -   - 
             * ...
             * 360  -   -   -   -   -   -   -   -   -   - 
             * 370  -   -   -   A   A   A   -   -   -   -
             * 380  -   -   -   A   A   A   -   -   -   -
             * 390  -   -   -   A   A   A   -   -   -   -
             *
             * A: selection A
             */

            // source file
            var sourceFileName = $"{datasetName}.h5";

            if (File.Exists(sourceFileName))
                File.Delete(sourceFileName);

            var sourceFileId = H5F.create(sourceFileName, H5F.ACC_TRUNC);

            // source dataset
            var sourceSpaceId = H5S.create_simple(1, new ulong[] { 90 }, new ulong[] { 90 });
            var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
            var memorySpaceId = H5S.create_simple(1, new ulong[] { 90 }, new ulong[] { 90 });
            var data = TestData.MediumData.Skip(0).Take(90).ToArray();

            unsafe
            {
                fixed (int* ptr = data.AsSpan())
                {
                    _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
                }
            }

            // virtual dataset
            var virtualSpaceId = H5S.create_simple(2, new ulong[] { 40, 10 }, new ulong[] { 40, 10 });

            // virtual selection A
            _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.SET, 
                start: new ulong[] { 1, 3 }, 
                stride: new ulong[] { 4, 3 }, 
                count: new ulong[] { 10, 1 },
                block: new ulong[] { 3, 3 });

            // create virtual dataset
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            _ = H5P.set_virtual(dcpl_id, virtualSpaceId, sourceFileName, "/source", sourceSpaceId);

            var datasetId = H5D.create(fileId, "vds", H5T.NATIVE_INT32, virtualSpaceId, dcpl_id: dcpl_id);

            // clean up
            _ = H5S.close(memorySpaceId);
            _ = H5S.close(sourceSpaceId);
            _ = H5D.close(sourceDatasetId);
            _ = H5F.close(sourceFileId);

            _ = H5S.close(virtualSpaceId);
            _ = H5D.close(datasetId);

            _ = H5P.close(dcpl_id);
        }
    }
}