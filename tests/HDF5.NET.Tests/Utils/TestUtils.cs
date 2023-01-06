using HDF.PInvoke;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xunit.Abstractions;

namespace HDF5.NET.Tests
{
    public class TestUtils
    {
        #region Links

        public static unsafe void AddSomeLinks(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "simple");
            var groupId_sub = H5G.create(groupId, "sub");

            // datasets
            var dataspaceId1 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId1 = H5D.create(fileId, "D", H5T.NATIVE_INT8, dataspaceId1);
            var data1 = new byte[] { 1 };

            fixed (void* ptr = data1)
            {
                res = H5D.write(datasetId1, H5T.NATIVE_INT8, dataspaceId1, dataspaceId1, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId1);
            res = H5S.close(dataspaceId1);

            var dataspaceId2 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId2 = H5D.create(groupId, "D1", H5T.NATIVE_INT8, dataspaceId2);

            res = H5D.close(datasetId2);
            res = H5S.close(dataspaceId2);

            var dataspaceId3 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId3 = H5D.create(groupId_sub, "D1.1", H5T.NATIVE_INT8, dataspaceId3);

            res = H5D.close(datasetId3);
            res = H5S.close(dataspaceId3);

            res = H5G.close(groupId);
            res = H5G.close(groupId_sub);
        }

        public static unsafe void AddMassLinks(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "mass_links");

            for (int i = 0; i < 1000; i++)
            {
                var linkId = H5G.create(groupId, $"mass_{i.ToString("D4")}");
                res = H5G.close(linkId);
            }

            res = H5G.close(groupId);
        }

        public static unsafe void AddLinks(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "links");

            var hardLinkId1 = H5G.create(groupId, "hard_link_1");
            res = H5L.create_hard(groupId, "hard_link_1", groupId, "hard_link_2");
            res = H5L.create_soft("hard_link_2", groupId, "soft_link_1");
            res = H5L.create_soft("/links/soft_link_1", groupId, "soft_link_2");

            var spaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId = H5D.create(hardLinkId1, "dataset", H5T.NATIVE_INT, spaceId);

            res = H5L.create_soft("/links/soft_link_2/dataset", groupId, "dataset");

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);

            res = H5G.close(groupId);
            res = H5G.close(hardLinkId1);
        }

        public static unsafe void AddExternalFileLink(long fileId, string filePath)
        {
            long res;

            var groupId = H5G.create(fileId, "links");
            res = H5L.create_external(filePath, "/external/group", groupId, "external_link");
            res = H5G.close(groupId);
        }

        public static unsafe void AddCircularReference(long fileId)
        {
            long res;

            var groupId_parent = H5G.create(fileId, "circular");

            res = H5L.create_soft("/circular", groupId_parent, "soft");
            res = H5L.create_hard(fileId, "circular", groupId_parent, "hard");

            var groupId_child1 = H5G.create(groupId_parent, "child");
            var groupId_child2 = H5G.create(groupId_child1, "rainbow's end");

            res = H5G.close(groupId_child2);
            res = H5G.close(groupId_child1);
            res = H5G.close(groupId_parent);
        }

        #endregion

        #region Attributes

        public static unsafe void AddHuge(long fileId, ContainerType container, H5F.libver_t version)
        {
            var length = version switch
            {
                H5F.libver_t.EARLIEST => 16368UL, // max 64 kb in object header
                _ => (ulong)TestData.HugeData.Length,
            };

            TestUtils.Add(container, fileId, "huge", "huge", H5T.NATIVE_INT32, TestData.HugeData.AsSpan(), length);
        }

        public static unsafe void AddTiny(long fileId, ContainerType container)
        {
            TestUtils.Add(container, fileId, "tiny", "tiny", H5T.NATIVE_UINT8, TestData.TinyData.AsSpan());
        }

        #endregion

        #region Datasets

        public static unsafe void AddExternalDataset(long fileId, string datasetName, string absolutePrefix, H5DatasetAccess datasetAccess)
        {
            long res;

            var bytesoftype = 4;
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            var dapl_id = H5P.create(H5P.DATASET_ACCESS);

            res = H5P.set_layout(dcpl_id, H5D.layout_t.CONTIGUOUS);

            // a (more than one chunk in file)
            var pathA = H5Utils.ConstructExternalFilePath(Path.Combine(absolutePrefix, $"{datasetName}_a.raw"), datasetAccess);

            if (File.Exists(pathA))
                File.Delete(pathA);

            res = H5P.set_external(dcpl_id, pathA, new IntPtr(120), (ulong)(10 * bytesoftype));
            res = H5P.set_external(dcpl_id, pathA, new IntPtr(80), (ulong)(10 * bytesoftype));
            res = H5P.set_external(dcpl_id, pathA, new IntPtr(0), (ulong)(10 * bytesoftype));

            // b (file size smaller than set size)
            var pathB = H5Utils.ConstructExternalFilePath(Path.Combine(absolutePrefix, $"{datasetName}_b.raw"), datasetAccess);

            if (File.Exists(pathB))
                File.Delete(pathB);

            res = H5P.set_external(dcpl_id, pathB, new IntPtr(0), (ulong)(10 * bytesoftype));

            // c (normal file)
            var pathC = H5Utils.ConstructExternalFilePath(Path.Combine(absolutePrefix, $"{datasetName}_c.raw"), datasetAccess);

            if (File.Exists(pathC))
                File.Delete(pathC);

            res = H5P.set_external(dcpl_id, pathC, new IntPtr(0), (ulong)((TestData.MediumData.Length - 40) * bytesoftype));

            // write data
            if (datasetAccess.ExternalFilePrefix is not null)
                H5P.set_efile_prefix(dapl_id, datasetAccess.ExternalFilePrefix);

            TestUtils.Add(ContainerType.Dataset, fileId, "external", datasetName, H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), apl: dapl_id, cpl: dcpl_id);

            // truncate file b
            using (var fileStream2 = File.OpenWrite(pathB))
            {
                fileStream2.SetLength(10);
            };

            res = H5P.close(dapl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddCompactDataset(long fileId)
        {
            long res;

            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_layout(dcpl_id, H5D.layout_t.COMPACT);
            TestUtils.Add(ContainerType.Dataset, fileId, "compact", "compact", H5T.NATIVE_INT32, TestData.SmallData.AsSpan(), cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddContiguousDataset(long fileId)
        {
            TestUtils.Add(ContainerType.Dataset, fileId, "contiguous", "contiguous", H5T.NATIVE_INT32, TestData.HugeData.AsSpan());
        }

        public static unsafe void AddContiguousDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length;
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            res = H5P.set_layout(dcpl_id, H5D.layout_t.CONTIGUOUS);

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            TestUtils.Add(ContainerType.Dataset, fileId, "fillvalue", $"{LayoutClass.Contiguous}", H5T.NATIVE_INT32, (void*)0, length, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDatasetForHyperslab(long fileId)
        {
            long res;

            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            var dims = new ulong[] { 25, 25, 4 };
            var chunkDims = new ulong[] { 7, 20, 3 };

            res = H5P.set_chunk(dcpl_id, 3, chunkDims);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "hyperslab", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Legacy(long fileId, bool withShuffle)
        {
            long res;

            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length;
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            res = H5P.set_chunk(dcpl_id, 1, new ulong[] { 1000 });

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            TestUtils.Add(ContainerType.Dataset, fileId, "fillvalue", $"{LayoutClass.Chunked}", H5T.NATIVE_INT32, (void*)0, length, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Single_Chunk(long fileId, bool withShuffle)
        {
            long res;

            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { length, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_single_chunk", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Implicit(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });
            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.EARLY);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_implicit", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);
           
            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_fixed_array", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array_Paged(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_fixed_array_paged", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Elements(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_extensible_array_elements", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Data_Blocks(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 100, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_extensible_array_data_blocks", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Secondary_Blocks(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 3, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_extensible_array_secondary_blocks", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_BTree2(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, H5S.UNLIMITED };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_btree2", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);

            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Huge(long fileId)
        {
            long res;

            var length = (ulong)TestData.HugeData.Length;
            var dims = new ulong[] { length };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 1, new ulong[] { 1_000_000 });
            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.EARLY);

            TestUtils.Add(ContainerType.Dataset, fileId, "chunked", "chunked_huge", H5T.NATIVE_INT32, TestData.HugeData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddVirtualDataset(long fileId, string datasetName, string absolutePrefix, H5DatasetAccess datasetAccess)
        {
            // see VirtualMapping.xlsx for a visualization

            long res;

            var vspaceId = H5S.create_simple(1, new ulong[] { 20 }, new ulong[] { 20 });
            var dapl_id = H5P.create(H5P.DATASET_ACCESS);
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            unsafe
            {
                var value = -1;
                res = H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT32, new IntPtr(&value));
            }

            // file 1
            var fileName1 = $"{datasetName}_a.h5";
            var path1 = H5Utils.ConstructExternalFilePath(Path.Combine(absolutePrefix, fileName1), datasetAccess);

            if (File.Exists(path1))
                File.Delete(path1);

            var fileId1 = H5F.create(path1, H5F.ACC_TRUNC);
            var dataA = TestData.SmallData.Skip(0).Take(16).ToArray();
            TestUtils.Add(ContainerType.Dataset, fileId1, "vds", "source_a", H5T.NATIVE_INT32, dataA.AsSpan());

            var sourceSpaceId1 = H5S.create_simple(1, new ulong[] { (ulong)dataA.Length }, new ulong[] { (ulong)dataA.Length });

            res = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, new ulong[] { 3 }, new ulong[] { 5 }, new ulong[] { 2 }, new ulong[] { 3 });
            res = H5S.select_hyperslab(sourceSpaceId1, H5S.seloper_t.SET, new ulong[] { 2 }, new ulong[] { 5 }, new ulong[] { 3 }, new ulong[] { 2 });
            res = H5P.set_virtual(dcpl_id, vspaceId, fileName1, "/vds/source_a", sourceSpaceId1);

            // file 2
            var fileName2 = $"{datasetName}_b.h5";
            var path2 = H5Utils.ConstructExternalFilePath(Path.Combine(absolutePrefix, $"{datasetName}_b.h5"), datasetAccess);

            if (File.Exists(path2))
                File.Delete(path2);

            var fileId2 = H5F.create(path2, H5F.ACC_TRUNC);
            var dataB = TestData.SmallData.Skip(10).Take(16).ToArray();
            TestUtils.Add(ContainerType.Dataset, fileId2, "vds", "source_b", H5T.NATIVE_INT32, dataB.AsSpan());

            var sourceSpaceId2 = H5S.create_simple(1, new ulong[] { (ulong)dataB.Length }, new ulong[] { (ulong)dataB.Length });

            res = H5S.select_hyperslab(vspaceId, H5S.seloper_t.SET, new ulong[] { 6 }, new ulong[] { 5 }, new ulong[] { 2 }, new ulong[] { 2 });
            res = H5S.select_hyperslab(sourceSpaceId2, H5S.seloper_t.SET, new ulong[] { 3 }, new ulong[] { 4 }, new ulong[] { 4 }, new ulong[] { 1 });
            res = H5P.set_virtual(dcpl_id, vspaceId, fileName2, "/vds/source_b", sourceSpaceId2);

            // create virtual dataset
            var datasetId = H5D.create(fileId, "vds", H5T.NATIVE_INT32, vspaceId, dcpl_id: dcpl_id, dapl_id: dapl_id);

            H5S.close(sourceSpaceId1);
            H5F.close(fileId1);

            H5S.close(sourceSpaceId2);
            H5F.close(fileId2);

            H5S.close(vspaceId);
            H5D.close(datasetId);

            res = H5P.close(dapl_id);
            res = H5P.close(dcpl_id);
        }


        #endregion

        #region Filter

        public static unsafe void AddFilteredDataset_Shuffle(long fileId, int bytesOfType, int length, Span<byte> dataset)
        {
            long res;

            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 1, new ulong[] { (ulong)length });
            res = H5P.set_shuffle(dcpl_id);

            var typeId = bytesOfType switch
            {
                1 => H5T.NATIVE_UINT8,
                2 => H5T.NATIVE_UINT16,
                4 => H5T.NATIVE_UINT32,
                8 => H5T.NATIVE_UINT64,
                _ => throw new Exception($"The value '{bytesOfType}' of the 'bytesOfType' parameter is not within the valid range.")
            };

            TestUtils.Add(ContainerType.Dataset, fileId, "filtered", $"shuffle_{bytesOfType}", typeId, dataset, (ulong)length, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddFilteredDataset_ZLib(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_filter(dcpl_id, H5Z.filter_t.DEFLATE, 0, new IntPtr(1), new uint[] { 5 } /* compression level */);

            TestUtils.Add(ContainerType.Dataset, fileId, "filtered", $"deflate", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddFilteredDataset_Fletcher(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_fletcher32(dcpl_id);

            TestUtils.Add(ContainerType.Dataset, fileId, "filtered", $"fletcher", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            res = H5P.close(dcpl_id);
        }

        public static unsafe void AddFilteredDataset_Multi(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_fletcher32(dcpl_id);
            res = H5P.set_shuffle(dcpl_id);
            res = H5P.set_deflate(dcpl_id, level: 5);

            TestUtils.Add(ContainerType.Dataset, fileId, "filtered", $"multi", H5T.NATIVE_INT32, TestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
        }

        #endregion

        #region Shared

        public static unsafe void AddDataspaceScalar(long fileId, ContainerType container)
        {
            var spaceId = H5S.create(H5S.class_t.SCALAR);
            var data = new double[] { -1.2234234e-3 };

            fixed (void* dataPtr = data)
            {
                TestUtils.Add(container, fileId, "dataspace", "scalar", H5T.NATIVE_DOUBLE, dataPtr, spaceId);
            }

            H5S.close(spaceId);
        }

        public static unsafe void AddDataspaceNull(long fileId, ContainerType container)
        {
            var spaceId = H5S.create(H5S.class_t.NULL);
            TestUtils.Add(container, fileId, "dataspace", "null", H5T.NATIVE_DOUBLE, null, spaceId);
            H5S.close(spaceId);
        }

        public static unsafe void AddNumerical(long fileId, ContainerType container)
        {
            var dims = new ulong[] { 2, 2, 3 };

            foreach (var entry in TestData.NumericalData)
            {
                var attributeData = (Array)entry[1];
                var type = attributeData
                    .GetType()
                    .GetElementType();

                var typeId = TestUtils.GetHdfTypeIdFromType(type);

                if (type == typeof(TestEnum))
                {
                    attributeData = attributeData
                        .OfType<object>()
                        .Select(value => (short)value)
                        .ToArray();
                }

                var handle = GCHandle.Alloc(attributeData, GCHandleType.Pinned);
                var ptr = handle.AddrOfPinnedObject().ToPointer();

                TestUtils.Add(container, fileId, "numerical", (string)entry[0], typeId, ptr, dims);

                handle.Free();
            }
        }

        public static unsafe void AddBitField(long fileId, ContainerType container)
        {
            TestUtils.Add(container, fileId, "bitfield", "bitfield", H5T.STD_B16LE, TestData.BitfieldData.AsSpan());
        }

        public static unsafe void AddOpaque(long fileId, ContainerType container)
        {
            long res;

            var length = (ulong)TestData.SmallData.Length * 2;
            var typeId = H5T.create(H5T.class_t.OPAQUE, new IntPtr(2));
            res = H5T.set_tag(typeId, "Opaque Test Tag");

            TestUtils.Add(container, fileId, "opaque", "opaque", typeId, TestData.SmallData.AsSpan(), length);

            res = H5T.close(typeId);
        }

        public static unsafe void AddArray(long fileId, ContainerType container)
        {
            long res;

            var typeId = H5T.array_create(H5T.NATIVE_INT32, 2, new ulong[] { 4, 5 });
            var dims = new ulong[] { 2, 3 };

            fixed (void* dataPtr = TestData.ArrayData)
            {
                TestUtils.Add(container, fileId, "array", "array", typeId, dataPtr, dims);
            }

            res = H5T.close(typeId);
        }

        public static unsafe void AddObjectReference(long fileId, ContainerType container)
        {
            long res;

            TestUtils.AddNumerical(fileId, ContainerType.Dataset);

            var length = (ulong)TestData.NumericalData.Count;
            var data = new ulong[length];

            fixed (ulong* ptr = data)
            {
                var referenceGroupId = H5G.open(fileId, "numerical");

                for (ulong i = 0; i < length; i++)
                {
                    res = H5R.create(new IntPtr(ptr + i), referenceGroupId, $"D{i + 1}", H5R.type_t.OBJECT, -1);
                }

                TestUtils.Add(container, fileId, "reference", "object_reference", H5T.STD_REF_OBJ, new IntPtr(ptr).ToPointer(), length);

                res = H5G.close(referenceGroupId);
            }
        }

        public static unsafe void AddRegionReference(long fileId, ContainerType container)
        {
            long res;

            TestUtils.AddSmall(fileId, ContainerType.Dataset);

            var length = 1UL;
            var data = new ulong[length];

            fixed (ulong* ptr = data)
            {
                var referenceGroupId = H5G.open(fileId, "small");
                var spaceId = H5S.create_simple(1, new ulong[] { length }, null);
                var coordinates = new ulong[] { 2, 4, 6, 8 };
                res = H5S.select_elements(spaceId, H5S.seloper_t.SET, new IntPtr(4), coordinates);
                res = H5R.create(new IntPtr(ptr), referenceGroupId, "small", H5R.type_t.DATASET_REGION, spaceId);

                TestUtils.Add(container, fileId, "reference", "region_reference", H5T.STD_REF_DSETREG, new IntPtr(ptr).ToPointer(), length);

                res = H5S.close(spaceId);
                res = H5G.close(referenceGroupId);
            }
        }

        public static unsafe void AddStruct(long fileId, ContainerType container)
        {
            long res;

            var dims = new ulong[] { 2, 2, 3 }; /* "extendible contiguous non-external dataset not allowed" */

            // non-nullable struct
            var typeId = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));
            TestUtils.Add(container, fileId, "struct", "nonnullable", typeId, TestData.NonNullableStructData.AsSpan(), dims);
            res = H5T.close(typeId);

            // nullable struct
            var typeIdNullable = TestUtils.GetHdfTypeIdFromType(typeof(TestStructStringAndArray));
            var dataNullable = TestData.StringStructData;

            // There is also Unsafe.SizeOf<T>() to calculate managed size instead of native size.
            // Is only relevant when Marshal.XX methods are replaced by other code.
            var elementSize = Marshal.SizeOf<TestStructStringAndArray>();
            var totalByteLength = elementSize * dataNullable.Length;
            var dataNullablePtr = Marshal.AllocHGlobal(totalByteLength);
            var counter = 0;

            dataNullable.Cast<ValueType>().ToList().ForEach(x =>
            {
                var sourcePtr = Marshal.AllocHGlobal(elementSize);
                Marshal.StructureToPtr(x, sourcePtr, false);

                var source = new Span<byte>(sourcePtr.ToPointer(), elementSize);
                var target = new Span<byte>(IntPtr.Add(dataNullablePtr, elementSize * counter).ToPointer(), elementSize);

                source.CopyTo(target);
                counter++;
                Marshal.FreeHGlobal(sourcePtr);
            });

            TestUtils.Add(container, fileId, "struct", "nullable", typeIdNullable, dataNullablePtr.ToPointer(), dims);

            Marshal.FreeHGlobal(dataNullablePtr);
            res = H5T.close(typeIdNullable);
        }

        public static unsafe void AddString(long fileId, ContainerType container)
        {
            long res;

            var dims = new ulong[] { 2, 2, 3 }; /* "extendible contiguous non-external dataset not allowed" */

            // fixed length string + null terminate (ASCII)
            var typeIdFixed_nullterm = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdFixed_nullterm, new IntPtr(4));
            res = H5T.set_cset(typeIdFixed_nullterm, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdFixed_nullterm, H5T.str_t.NULLTERM);

            var dataFixed_nullterm = new string[] { "00\00", "11\0 ", "22\0 ", "3\0  ", "44 \0", "555\0", "66 \0", "77\0 ", "  \0 ", "AA \0", "ZZ \0", "!!\0 " };
            var dataFixedChar_nullterm = dataFixed_nullterm
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            TestUtils.Add(container, fileId, "string", "fixed+nullterm", typeIdFixed_nullterm, dataFixedChar_nullterm.AsSpan(), dims);

            res = H5T.close(typeIdFixed_nullterm);

            // fixed length string + null padding (ASCII)
            var typeIdFixed_nullpad = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdFixed_nullpad, new IntPtr(4));
            res = H5T.set_cset(typeIdFixed_nullpad, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdFixed_nullpad, H5T.str_t.NULLPAD);

            var dataFixed_nullpad = new string[] { "0\00\0", "11\0\0", "22\0\0", "3 \0\0", " 4\0\0", "55 5", "66\0\0", "77\0\0", "  \0\0", "AA\0\0", "ZZ\0\0", "!!\0\0" };
            var dataFixedChar_nullpad = dataFixed_nullpad
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            TestUtils.Add(container, fileId, "string", "fixed+nullpad", typeIdFixed_nullpad, dataFixedChar_nullpad.AsSpan(), dims);

            res = H5T.close(typeIdFixed_nullpad);

            // fixed length string + space padding (ASCII)
            var typeIdFixed_spacepad = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdFixed_spacepad, new IntPtr(4));
            res = H5T.set_cset(typeIdFixed_spacepad, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdFixed_spacepad, H5T.str_t.SPACEPAD);

            var dataFixed_spacepad = new string[] { "00  ", "11  ", "22  ", "3   ", " 4  ", "55 5", "66  ", "77  ", "    ", "AA  ", "ZZ  ", "!!  " };
            var dataFixedChar_spacepad = dataFixed_spacepad
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            TestUtils.Add(container, fileId, "string", "fixed+spacepad", typeIdFixed_spacepad, dataFixedChar_spacepad.AsSpan(), dims);

            res = H5T.close(typeIdFixed_spacepad);

            // variable length string (ASCII)
            var typeIdVar = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdVar, H5T.VARIABLE);
            res = H5T.set_cset(typeIdVar, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdVar, H5T.str_t.NULLPAD);

            var dataVar = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var dataVarChar = dataVar
               .SelectMany(value => Encoding.ASCII.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtr = dataVarChar)
            {
                var basePtr = new IntPtr(dataVarPtr);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 4), IntPtr.Add(basePtr, 7), IntPtr.Add(basePtr, 10),
                    IntPtr.Add(basePtr, 13), IntPtr.Add(basePtr, 16), IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22),
                    IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28), IntPtr.Add(basePtr, 31), IntPtr.Add(basePtr, 34)
                };

                fixed (void* dataVarAddressesPtr = addresses)
                {
                    TestUtils.Add(container, fileId, "string", "variable", typeIdVar, dataVarAddressesPtr, dims);
                }
            }

            res = H5T.close(typeIdVar);

            // variable length string + space padding (ASCII)
            var typeIdVar_spacepad = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdVar_spacepad, H5T.VARIABLE);
            res = H5T.set_cset(typeIdVar_spacepad, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdVar_spacepad, H5T.str_t.SPACEPAD);

            var dataVar_spacepad = new string[] { "001  ", "1 1 ", "22  ", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var dataVarChar_spacepad = dataVar_spacepad
               .SelectMany(value => Encoding.ASCII.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtr_spacepad = dataVarChar_spacepad)
            {
                var basePtr = new IntPtr(dataVarPtr_spacepad);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 6), IntPtr.Add(basePtr, 11), IntPtr.Add(basePtr, 16),
                    IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22), IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28),
                    IntPtr.Add(basePtr, 31), IntPtr.Add(basePtr, 34), IntPtr.Add(basePtr, 37), IntPtr.Add(basePtr, 40)
                };

                fixed (void* addressesPtr = addresses)
                {
                    TestUtils.Add(container, fileId, "string", "variable+spacepad", typeIdVar_spacepad, addressesPtr, dims);
                }
            }

            res = H5T.close(typeIdVar);

            // variable length string attribute (UTF8)
            var typeIdVarUTF8 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdVarUTF8, H5T.VARIABLE);
            res = H5T.set_cset(typeIdVarUTF8, H5T.cset_t.UTF8);
            res = H5T.set_strpad(typeIdVarUTF8, H5T.str_t.NULLPAD);

            var dataVarUTF8 = new string[] { "00", "111", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" };
            var dataVarCharUTF8 = dataVarUTF8
               .SelectMany(value => Encoding.UTF8.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtrUTF8 = dataVarCharUTF8)
            {
                var basePtr = new IntPtr(dataVarPtrUTF8);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 3), IntPtr.Add(basePtr, 7), IntPtr.Add(basePtr, 10),
                    IntPtr.Add(basePtr, 13), IntPtr.Add(basePtr, 16), IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22),
                    IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28), IntPtr.Add(basePtr, 33), IntPtr.Add(basePtr, 40)
                };

                fixed (void* addressesPtr = addresses)
                {
                    TestUtils.Add(container, fileId, "string", "variableUTF8", typeIdVarUTF8, addressesPtr, dims);
                }
            }
        }

        public static unsafe void AddMass(long fileId, ContainerType container)
        {
            long res;

            var typeId = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));

            for (int i = 0; i < 1000; i++)
            {
                var dims = new ulong[] { 2, 2, 3 };

                if (i == 450)
                {
                    var acpl_id = H5P.create(H5P.ATTRIBUTE_CREATE);
                    res = H5P.set_char_encoding(acpl_id, H5T.cset_t.UTF8);
                    var name = "字形碼 / 字形码, Zìxíngmǎ";
                    TestUtils.Add(container, fileId, "mass_attributes", name, typeId, TestData.NonNullableStructData.AsSpan(), dims, cpl: acpl_id);
                }
                else
                {
                    var name = $"mass_{i.ToString("D4")}";
                    TestUtils.Add(container, fileId, "mass_attributes", name, typeId, TestData.NonNullableStructData.AsSpan(), dims);
                }
            }

            res = H5T.close(typeId);
        }

        public static unsafe void AddSmall(long fileId, ContainerType container)
        {
            TestUtils.Add(container, fileId, "small", "small", H5T.NATIVE_INT32, TestData.SmallData.AsSpan());
        }

        public static unsafe void AddDataWithSharedDataType(long fileId, ContainerType container)
        {
            long typeId = H5T.copy(H5T.C_S1);

            H5T.set_size(typeId, H5T.VARIABLE);
            H5T.set_cset(typeId, H5T.cset_t.UTF8);
            H5T.commit(fileId, "string_t", typeId);

            var data = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var dataChar = data
               .SelectMany(value => Encoding.ASCII.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtr = dataChar)
            {
                var basePtr = new IntPtr(dataVarPtr);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 4), IntPtr.Add(basePtr, 7), IntPtr.Add(basePtr, 10),
                    IntPtr.Add(basePtr, 13), IntPtr.Add(basePtr, 16), IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22),
                    IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28), IntPtr.Add(basePtr, 31), IntPtr.Add(basePtr, 34)
                };

                fixed (void* dataVarAddressesPtr = addresses)
                {
                    TestUtils.Add(container, fileId, "shared_data_type", "shared_data_type", typeId, dataVarAddressesPtr, length: 12);
                }
            }

            if (H5I.is_valid(typeId) > 0) { H5T.close(typeId); }
        }

        #endregion

        #region Helpers

        public static void RunForAllVersions(Action<H5F.libver_t> action)
        {
            var versions = new H5F.libver_t[]
            {
                H5F.libver_t.V18,
                H5F.libver_t.V110
            };

            foreach (var version in versions)
            {
                action(version);
            }
        }

        public static void RunForVersions(H5F.libver_t[] versions, Action<H5F.libver_t> action)
        {
            foreach (var version in versions)
            {
                action(version);
            }
        }

        public static unsafe string PrepareTestFile(H5F.libver_t version, Action<long> action)
        {
            var filePath = Path.GetTempFileName();
            long res;

            // file
            var faplId = H5P.create(H5P.FILE_ACCESS);
            res = H5P.set_libver_bounds(faplId, version, version);
            var fileId = H5F.create(filePath, H5F.ACC_TRUNC, 0, faplId);
            action?.Invoke(fileId);
            res = H5F.close(fileId);

            return filePath;
        }

        public static unsafe void Add<T>(ContainerType container, long fileId, string groupName, string elementName, long typeId, Span<T> data, long cpl = 0, long apl = 0)
            where T : unmanaged
        {
            var length = (ulong)data.Length;
            TestUtils.Add(container, fileId, groupName, elementName, typeId, data, length, cpl, apl);
        }

        public static unsafe void Add<T>(ContainerType container, long fileId, string groupName, string elementName, long typeId, Span<T> data, ulong length, long cpl = 0, long apl = 0)
            where T : unmanaged
        {
            var dims0 = new ulong[] { length };
            TestUtils.Add(container, fileId, groupName, elementName, typeId, data, dims0, dims0, cpl, apl);
        }

        public static unsafe void Add<T>(ContainerType container, long fileId, string groupName, string elementName, long typeId, Span<T> data, ulong[] dims0, ulong[] dims1 = null, long cpl = 0, long apl = 0)
            where T : unmanaged
        {
            fixed (void* dataPtr = data)
            {
                TestUtils.Add(container, fileId, groupName, elementName, typeId, dataPtr, dims0, dims1, cpl, apl);
            }
        }

        public static unsafe void Add(ContainerType container, long fileId, string groupName, string elementName, long typeId, void* dataPtr, ulong length, long cpl = 0, long apl = 0)
        {
            var dims0 = new ulong[] { length };
            TestUtils.Add(container, fileId, groupName, elementName, typeId, dataPtr, dims0, dims0, cpl, apl);
        }

        public static unsafe void Add(ContainerType container, long fileId, string groupName, string elementName, long typeId, void* dataPtr, ulong[] dims0, ulong[] dims1 = null, long cpl = 0, long apl = 0)
        {
            long res;

            if (dims1 is null)
                dims1 = dims0;

            var spaceId = H5S.create_simple(dims0.Length, dims0, dims1);
            TestUtils.Add(container, fileId, groupName, elementName, typeId, dataPtr, spaceId, cpl, apl);
            res = H5S.close(spaceId);
        }

        public static unsafe void Add(ContainerType container, long fileId, string groupName, string elementName, long typeId, void* dataPtr, long spaceId, long cpl = 0, long apl = 0)
        {
            long res;
            long groupId;

            if (H5L.exists(fileId, groupName) > 0)
                groupId = H5G.open(fileId, groupName);
            else
                groupId = H5G.create(fileId, groupName);

            long id;

            if (container == ContainerType.Dataset)
            {
                id = H5D.create(groupId, Encoding.UTF8.GetBytes(elementName), typeId, spaceId, dcpl_id: cpl, dapl_id: apl);

                if (id == -1)
                    throw new Exception("Could not create dataset.");

                if ((int)dataPtr != 0)
                    res = H5D.write(id, typeId, spaceId, H5S.ALL, 0, new IntPtr(dataPtr));

                res = H5D.close(id);
            }
            else
            {
                id = H5A.create(groupId, Encoding.UTF8.GetBytes(elementName), typeId, spaceId, acpl_id: cpl);

                if (id == -1)
                    throw new Exception("Could not create attribute.");

                if ((int)dataPtr != 0)
                    res = H5A.write(id, typeId, new IntPtr(dataPtr));

                res = H5A.close(id);
            }

            res = H5G.close(groupId);
        }

        public static bool ReadAndCompare<T>(H5Dataset dataset, T[] expected) 
            where T : unmanaged
        {
            var actual = dataset.Read<T>();
            return actual.SequenceEqual(expected);
        }

        public static void CaptureHdfLibOutput(ITestOutputHelper logger)
        {
            H5E.set_auto(H5E.DEFAULT, ErrorDelegateMethod, IntPtr.Zero);

            int ErrorDelegateMethod(long estack, IntPtr client_data)
            {
                H5E.walk(estack, H5E.direction_t.H5E_WALK_DOWNWARD, WalkDelegateMethod, IntPtr.Zero);
                return 0;
            }

            int WalkDelegateMethod(uint n, ref H5E.error_t err_desc, IntPtr client_data)
            {
                logger.WriteLine($"{n}: {err_desc.desc}");
                return 0;
            }
        }

        private static long GetHdfTypeIdFromType(Type type, ulong? arrayLength = default)
        {
            if (type == typeof(bool))
                return H5T.NATIVE_UINT8;

            else if (type == typeof(byte))
                return H5T.NATIVE_UINT8;

            else if (type == typeof(sbyte))
                return H5T.NATIVE_INT8;

            else if (type == typeof(ushort))
                return H5T.NATIVE_UINT16;

            else if (type == typeof(short))
                return H5T.NATIVE_INT16;

            else if (type == typeof(uint))
                return H5T.NATIVE_UINT32;

            else if (type == typeof(int))
                return H5T.NATIVE_INT32;

            else if (type == typeof(ulong))
                return H5T.NATIVE_UINT64;

            else if (type == typeof(long))
                return H5T.NATIVE_INT64;

            else if (type == typeof(float))
                return H5T.NATIVE_FLOAT;

            else if (type == typeof(double))
                return H5T.NATIVE_DOUBLE;

            // issues: https://en.wikipedia.org/wiki/Long_double
            //else if (elementType == typeof(decimal))
            //    return H5T.NATIVE_LDOUBLE;

            else if (type.IsArray && arrayLength.HasValue)
            {
                var elementType = type.GetElementType()!;
                var dims = new ulong[] { arrayLength.Value };
                var typeId = H5T.array_create(TestUtils.GetHdfTypeIdFromType(elementType), rank: 1, dims);

                return typeId;
            }

            else if (type.IsEnum)
            {
                var baseTypeId = TestUtils.GetHdfTypeIdFromType(Enum.GetUnderlyingType(type));
                var typeId = H5T.enum_create(baseTypeId);

                foreach (var value in Enum.GetValues(type))
                {
                    var value_converted = Convert.ToInt64(value);
                    var name = Enum.GetName(type, value_converted);

                    var handle = GCHandle.Alloc(value_converted, GCHandleType.Pinned);
                    H5T.enum_insert(typeId, name, handle.AddrOfPinnedObject());
                }

                return typeId;
            }

            else if (type == typeof(string) || type == typeof(IntPtr))
            {
                var typeId = H5T.copy(H5T.C_S1);

                H5T.set_size(typeId, H5T.VARIABLE);
                H5T.set_cset(typeId, H5T.cset_t.UTF8);

                return typeId;
            }
            else if (type.IsValueType && !type.IsPrimitive)
            {
                var typeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(Marshal.SizeOf(type)));

                foreach (var fieldInfo in type.GetFields())
                {
                    var marshalAsAttribute = fieldInfo.GetCustomAttribute<MarshalAsAttribute>();

                    var arraySize = marshalAsAttribute is not null && marshalAsAttribute.Value == UnmanagedType.ByValArray
                        ? new Nullable<ulong>((ulong)marshalAsAttribute.SizeConst)
                        : null;

                    var fieldType = TestUtils.GetHdfTypeIdFromType(fieldInfo.FieldType, arraySize);
                    var nameAttribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    var hdfFieldName = nameAttribute is not null ? nameAttribute.Name : fieldInfo.Name;

                    H5T.insert(typeId, hdfFieldName, Marshal.OffsetOf(type, fieldInfo.Name), fieldType);

                    if (H5I.is_valid(fieldType) > 0)
                        H5T.close(fieldType);
                }

                return typeId;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        #endregion
    }
}