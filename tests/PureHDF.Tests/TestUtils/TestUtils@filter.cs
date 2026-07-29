using HDF.PInvoke;

namespace PureHDF.Tests;

public partial class TestUtils
{
    public static unsafe void AddFilteredDataset_Shuffle(long fileId, int bytesOfType, int length, Span<byte> dataset)
    {
        var dcpl_id = H5P.create(H5P.DATASET_CREATE);

        _ = H5P.set_chunk(dcpl_id, 1, [(ulong)length]);
        _ = H5P.set_shuffle(dcpl_id);

        var typeId = bytesOfType switch
        {
            1 => H5T.NATIVE_UINT8,
            2 => H5T.NATIVE_UINT16,
            4 => H5T.NATIVE_UINT32,
            8 => H5T.NATIVE_UINT64,
            16 => CreateOpaque16(), // H5T.NATIVE_LDOUBLE is 16 bytes on posix, 8 bytes on windows
            _ => throw new Exception($"The value '{bytesOfType}' of the 'bytesOfType' parameter is not within the valid range.")
        };

        try
        {
	        Add(ContainerType.Dataset, fileId, "filtered", $"shuffle_{bytesOfType}", typeId, dataset, (ulong)length, cpl: dcpl_id);
        }
        finally
        {
            _ = H5P.close(dcpl_id);

            if (bytesOfType == 16)
                _ = H5T.close(typeId);
        }

        static long CreateOpaque16()
        {
            var id = H5T.create(H5T.class_t.OPAQUE, new IntPtr(16));
            H5T.set_tag(id, "16-byte opaque");
            return id;
        }
    }

    public static unsafe void AddFilteredDataset_ZLib(long fileId)
    {
        var length = (ulong)SharedTestData.MediumData.Length / 4;
        var dims = new ulong[] { length, 4 };
        var dcpl_id = H5P.create(H5P.DATASET_CREATE);

        _ = H5P.set_chunk(dcpl_id, 2, [1000, 4]);
        _ = H5P.set_filter(dcpl_id, H5Z.filter_t.DEFLATE, 0, new IntPtr(1), [5] /* compression level */);

        Add(ContainerType.Dataset, fileId, "filtered", $"deflate", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
        _ = H5P.close(dcpl_id);
    }

    public static unsafe void AddFilteredDataset_Fletcher(long fileId)
    {
        var length = (ulong)SharedTestData.MediumData.Length / 4;
        var dims = new ulong[] { length, 4 };
        var dcpl_id = H5P.create(H5P.DATASET_CREATE);

        _ = H5P.set_chunk(dcpl_id, 2, [1000, 4]);
        _ = H5P.set_fletcher32(dcpl_id);

        Add(ContainerType.Dataset, fileId, "filtered", $"fletcher", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
        _ = H5P.close(dcpl_id);
    }

    public static unsafe void AddFilteredDataset_Multi(long fileId)
    {
        var length = (ulong)SharedTestData.MediumData.Length / 4;
        var dims = new ulong[] { length, 4 };
        var dcpl_id = H5P.create(H5P.DATASET_CREATE);

        _ = H5P.set_chunk(dcpl_id, 2, [1000, 4]);
        _ = H5P.set_fletcher32(dcpl_id);
        _ = H5P.set_shuffle(dcpl_id);
        _ = H5P.set_deflate(dcpl_id, level: 5);

        Add(ContainerType.Dataset, fileId, "filtered", $"multi", H5T.NATIVE_INT32, SharedTestData.MediumData.AsSpan(), dims, cpl: dcpl_id);
    }
}