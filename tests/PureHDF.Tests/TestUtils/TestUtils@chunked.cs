using System.Runtime.InteropServices;
using HDF.PInvoke;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddChunkedDatasetForHyperslab(long fileId)
        {
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            var dims = new ulong[] { 25, 25, 4 };
            var chunkDims = new ulong[] { 7, 20, 3 };

            _ = H5P.set_chunk(dcpl_id, 3, chunkDims);

            Add(ContainerType.Dataset, fileId, "chunked", "hyperslab", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Legacy(long fileId, bool withShuffle)
        {
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            var length = (ulong)SharedTestData.MediumData.Length;
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            _ = H5P.set_chunk(dcpl_id, 1, new ulong[] { 1000 });

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            _ = H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            Add(ContainerType.Dataset, fileId, "fillvalue", $"{LayoutClass.Chunked}", H5T.NATIVE_INT32, (void*)0, length, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Single_Chunk(long fileId, bool withShuffle)
        {
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { length, 4 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_single_chunk", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Implicit(long fileId)
        {
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });
            _ = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.EARLY);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_implicit", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array(long fileId, bool withShuffle)
        {
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_fixed_array", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array_Paged(long fileId, bool withShuffle)
        {
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims = new ulong[] { length, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1, 3 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_fixed_array_paged", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Elements(long fileId, bool withShuffle)
        {
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_extensible_array_elements", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Data_Blocks(long fileId, bool withShuffle)
        {
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 100, 3 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_extensible_array_data_blocks", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Secondary_Blocks(long fileId, bool withShuffle)
        {
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, 4 };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 3, 3 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_extensible_array_secondary_blocks", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_BTree2(long fileId, bool withShuffle)
        {
            var length = (ulong)SharedTestData.MediumData.Length / 4;
            var dims0 = new ulong[] { length, 4 };
            var dims1 = new ulong[] { H5S.UNLIMITED, H5S.UNLIMITED };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 3 });

            if (withShuffle)
                _ = H5P.set_shuffle(dcpl_id);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_btree2", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims0, dims1, cpl: dcpl_id);

            _ = H5P.close(dcpl_id);
        }

        public static unsafe void AddChunkedDataset_Huge(long fileId)
        {
            var length = (ulong)SharedTestData.HugeData.Length;
            var dims = new ulong[] { length };
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            _ = H5P.set_chunk(dcpl_id, 1, new ulong[] { 1_000_000 });
            _ = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.EARLY);

            Add(ContainerType.Dataset, fileId, "chunked", "chunked_huge", H5T.NATIVE_INT32, SharedTestData.HugeData.AsSpan(), dims, cpl: dcpl_id);
            _ = H5P.close(dcpl_id);
        }
    }
}