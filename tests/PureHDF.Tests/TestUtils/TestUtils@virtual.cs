using HDF.PInvoke;

namespace PureHDF.Tests;

public partial class TestUtils
{
    public static unsafe void AddVirtualDataset(long fileId, string datasetName)
    {
        // see VirtualMapping.ods for a visualization

        var vspaceId = H5S.create_simple(1, [20], [20]);
        var dcpl_id = H5P.create(H5P.DATASET_CREATE);

        unsafe
        {
            var value = -1;
            _ = H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT32, new IntPtr(&value));
        }

        // file 1
        var fileName1 = $"{datasetName}_a.h5";

        if (File.Exists(fileName1))
            File.Delete(fileName1);

        var fileId1 = H5F.create(fileName1, H5F.ACC_TRUNC);
        var dataA = SharedTestData.SmallData.Skip(0).Take(16).ToArray();
        Add(ContainerType.Dataset, fileId1, "vds", "source_a", H5T.NATIVE_INT32, dataA.AsSpan());

        var sourceSpaceId1 = H5S.create_simple(1, [(ulong)dataA.Length], [(ulong)dataA.Length]);

        _ = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, [3], [5], [2], [3]);
        _ = H5S.select_hyperslab(sourceSpaceId1, H5S.seloper_t.SET, [2], [5], [3], [2]);
        _ = H5P.set_virtual(dcpl_id, vspaceId, fileName1, "/vds/source_a", sourceSpaceId1);

        // file 2
        var fileName2 = $"{datasetName}_b.h5";

        if (File.Exists(fileName2))
            File.Delete(fileName2);

        var fileId2 = H5F.create(fileName2, H5F.ACC_TRUNC);
        var dataB = SharedTestData.SmallData.Skip(10).Take(16).ToArray();
        Add(ContainerType.Dataset, fileId2, "vds", "source_b", H5T.NATIVE_INT32, dataB.AsSpan());

        var sourceSpaceId2 = H5S.create_simple(1, [(ulong)dataB.Length], [(ulong)dataB.Length]);

        _ = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, [6], [5], [2], [2]);
        _ = H5S.select_hyperslab(sourceSpaceId2, H5S.seloper_t.SET, [3], [4], [4], [1]);
        _ = H5P.set_virtual(dcpl_id, vspaceId, fileName2, "/vds/source_b", sourceSpaceId2);

        // file 3 (non-existent)
        var fileName3 = $"{datasetName}_c.h5";
        var sourceSpaceId3 = H5S.create_simple(1, [10UL], [10UL]);

        _ = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, [15], [1], [1], [1]);
        _ = H5S.select_hyperslab(sourceSpaceId3, H5S.seloper_t.SET, [0], [1], [1], [1]);
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

    public static unsafe void AddVirtualDataset_source_point(long fileId, string datasetName)
    {
        /*
            *      0   1   2   3   4   5   6   7   8   9 
            *  00  -   -   -   -   -   -   -   -   -   - 
            *  10  -   -   -   A   -   -   -   -   -   - 
            *  20  -   -   -   -   B   -   -   -   -   - 
            *  30  -   -   -   -   -   C   -   -   -   - 
            *  40  -   -   -   -   -   -   -   -   -   - 
            *  50  -   -   -   H   G   -   -   -   -   I 
            *  60  -   -   -   -   -   -   -   -   -   - 
            *  70  -   -   -   -   -   -   -   -   -   - 
            *  80  F   -   -   -   -   -   -   -   -   -
            *  90  -   J   -   -   E   -   -   -   -   -
            * 100  -   -   -   -   -   -   -   -   -   -
            * 110  -   -   -   -   -   -   -   -   -   D
            * 120  -   -   -   -   -   -   -   -   -   -
            *
            * A: selection A
            */

        // source file
        var sourceFileName = $"{datasetName}.h5";

        if (File.Exists(sourceFileName))
            File.Delete(sourceFileName);

        var sourceFileId = H5F.create(sourceFileName, H5F.ACC_TRUNC);

        // source dataset
        var sourceSpaceId = H5S.create_simple(2, [13, 10], [13, 10]);
        var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
        var memorySpaceId = H5S.create_simple(1, [130], [130]);
        var data = SharedTestData.MediumData.Skip(0).Take(130).ToArray();

        unsafe
        {
            fixed (int* ptr = data.AsSpan())
            {
                _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }
        }

        // source selection A
        // _ = H5S.select_elements(sourceSpaceId, H5S.seloper_t.SET, num_elements: 10,
        //     new ulong[] { 
        //         01, 03,
        //         02, 04,
        //         03, 05,
        //         11, 09,
        //         09, 04,
        //         08, 00,
        //         05, 04,
        //         05, 03,
        //         05, 09,
        //         09, 01
        //         });

        /* Fake selection needed
            * Reason: https://github.com/HDFGroup/hdf5/blob/hdf5_1_10/src/H5Dvirtual.c#L175-L177
            * How does it help? The test method which calls this method must replace the serialized 
            * hyperslab selection by a point selection in the source file. This works as long as 
            * the serialized hyperslab selection is larger than the point selection.
            */

        /* combine many single-point hyperslab selection to produce a large serialized selection */
        var stride = new ulong[] { 1, 1 };
        var count = new ulong[] { 1, 1 };
        var block = new ulong[] { 1, 1 };

        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.SET, start: [01, 03], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [02, 04], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [03, 05], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [11, 09], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [09, 04], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [08, 00], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [05, 04], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [05, 03], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [05, 09], stride, count, block);
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR, start: [09, 01], stride, count, block);

        // virtual dataset
        var virtualSpaceId = H5S.create_simple(1, [10], [10]);

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
        var sourceSpaceId = H5S.create_simple(2, [13, 10], [13, 10]);
        var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
        var memorySpaceId = H5S.create_simple(1, [130], [130]);
        var data = SharedTestData.MediumData.Skip(0).Take(130).ToArray();

        unsafe
        {
            fixed (int* ptr = data.AsSpan())
            {
                _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }
        }

        // source selection A
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.SET,
            start: [6, 1],
            stride: [3, 4],
            count: [1, 2],
            block: [3, 3]
        );

        // source selection B
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.OR,
            start: [1, 3],
            stride: [4, 3],
            count: [3, 1],
            block: [3, 3]
        );

        // virtual dataset
        var virtualSpaceId = H5S.create_simple(1, [41], [41]);

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
        var sourceSpaceId = H5S.create_simple(2, [40, 10], [40, 10]);
        var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
        var memorySpaceId = H5S.create_simple(1, [400], [400]);
        var data = SharedTestData.MediumData.Skip(0).Take(400).ToArray();

        unsafe
        {
            fixed (int* ptr = data.AsSpan())
            {
                _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }
        }

        // source selection A
        _ = H5S.select_hyperslab(sourceSpaceId, H5S.seloper_t.SET,
            start: [1, 3],
            stride: [4, 3],
            count: [10, 1],
            block: [3, 3]
        );

        // virtual dataset
        var virtualSpaceId = H5S.create_simple(1, [90], [90]);

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

    public static unsafe void AddVirtualDataset_virtual_point(long fileId, string datasetName)
    {
        /*
            *      0   1   2   3   4   5   6   7   8   9 
            *  00  -   -   -   -   -   -   -   -   -   - 
            *  10  -   -   -   A   -   -   -   -   -   - 
            *  20  -   -   -   -   B   -   -   -   -   - 
            *  30  -   -   -   -   -   C   -   -   -   - 
            *  40  -   -   -   -   -   -   -   -   -   - 
            *  50  -   -   -   H   G   -   -   -   -   I 
            *  60  -   -   -   -   -   -   -   -   -   - 
            *  70  -   -   -   -   -   -   -   -   -   - 
            *  80  F   -   -   -   -   -   -   -   -   -
            *  90  -   J   -   -   E   -   -   -   -   -
            * 100  -   -   -   -   -   -   -   -   -   -
            * 110  -   -   -   -   -   -   -   -   -   D
            * 120  -   -   -   -   -   -   -   -   -   -
            *
            * A: selection A
            */

        // source file
        var sourceFileName = $"{datasetName}.h5";

        if (File.Exists(sourceFileName))
            File.Delete(sourceFileName);

        var sourceFileId = H5F.create(sourceFileName, H5F.ACC_TRUNC);

        // source dataset
        var sourceSpaceId = H5S.create_simple(1, [10], [10]);
        var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
        var memorySpaceId = H5S.create_simple(1, [10], [10]);
        var data = SharedTestData.MediumData.Skip(0).Take(10).ToArray();

        unsafe
        {
            fixed (int* ptr = data.AsSpan())
            {
                _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }
        }

        // virtual dataset
        var virtualSpaceId = H5S.create_simple(2, [13, 10], [13, 10]);

        // virtual selection A
        // _ = H5S.select_elements(virtualSpaceId, H5S.seloper_t.SET, num_elements: 10,
        //     new ulong[] { 
        //         01, 03,
        //         02, 04,
        //         03, 05,
        //         11, 09,
        //         09, 04,
        //         08, 00,
        //         05, 04,
        //         05, 03,
        //         05, 09,
        //         09, 01
        //         });

        /* Fake selection needed
            * Reason: https://github.com/HDFGroup/hdf5/blob/hdf5_1_10/src/H5Dvirtual.c#L175-L177
            * How does it help? The test method which calls this method must replace the serialized 
            * hyperslab selection by a point selection in the source file. This works as long as 
            * the serialized hyperslab selection is larger than the point selection.
            */

        /* combine many single-point hyperslab selection to produce a large serialized selection */
        var stride = new ulong[] { 1, 1 };
        var count = new ulong[] { 1, 1 };
        var block = new ulong[] { 1, 1 };

        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.SET, start: [01, 03], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [02, 04], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [03, 05], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [11, 09], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [09, 04], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [08, 00], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [05, 04], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [05, 03], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [05, 09], stride, count, block);
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR, start: [09, 01], stride, count, block);

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
        var sourceSpaceId = H5S.create_simple(1, [41], [41]);
        var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
        var memorySpaceId = H5S.create_simple(1, [41], [41]);
        var data = SharedTestData.MediumData.Skip(0).Take(41).ToArray();

        unsafe
        {
            fixed (int* ptr = data.AsSpan())
            {
                _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }
        }

        // virtual dataset
        var virtualSpaceId = H5S.create_simple(2, [13, 10], [13, 10]);

        // virtual selection A
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.SET,
            start: [6, 1],
            stride: [3, 4],
            count: [1, 2],
            block: [3, 3]
        );

        // virtual selection B
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.OR,
            start: [1, 3],
            stride: [4, 3],
            count: [3, 1],
            block: [3, 3]
        );

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
        var sourceSpaceId = H5S.create_simple(1, [90], [90]);
        var sourceDatasetId = H5D.create(sourceFileId, "source", H5T.NATIVE_INT32, sourceSpaceId);
        var memorySpaceId = H5S.create_simple(1, [90], [90]);
        var data = SharedTestData.MediumData.Skip(0).Take(90).ToArray();

        unsafe
        {
            fixed (int* ptr = data.AsSpan())
            {
                _ = H5D.write(sourceDatasetId, H5T.NATIVE_INT32, memorySpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }
        }

        // virtual dataset
        var virtualSpaceId = H5S.create_simple(2, [40, 10], [40, 10]);

        // virtual selection A
        _ = H5S.select_hyperslab(virtualSpaceId, H5S.seloper_t.SET,
            start: [1, 3],
            stride: [4, 3],
            count: [10, 1],
            block: [3, 3]
        );

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