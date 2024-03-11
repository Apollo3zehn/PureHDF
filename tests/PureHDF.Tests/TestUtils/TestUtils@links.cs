using HDF.PInvoke;

namespace PureHDF.Tests;

public partial class TestUtils
{
    public static unsafe void AddSomeLinks(long fileId)
    {
        long res;

        var groupId = H5G.create(fileId, "simple");
        var groupId_sub = H5G.create(groupId, "sub");

        // datasets
        var dataspaceId1 = H5S.create_simple(1, [1], [1]);
        var datasetId1 = H5D.create(fileId, "D", H5T.NATIVE_INT8, dataspaceId1);
        var data1 = new byte[] { 1 };

        fixed (void* ptr = data1)
        {
            res = H5D.write(datasetId1, H5T.NATIVE_INT8, dataspaceId1, dataspaceId1, 0, new IntPtr(ptr));
        }

        res = H5D.close(datasetId1);
        res = H5S.close(dataspaceId1);

        var dataspaceId2 = H5S.create_simple(1, [1], [1]);
        var datasetId2 = H5D.create(groupId, "D1", H5T.NATIVE_INT8, dataspaceId2);

        res = H5D.close(datasetId2);
        res = H5S.close(dataspaceId2);

        var dataspaceId3 = H5S.create_simple(1, [1], [1]);
        var datasetId3 = H5D.create(groupId_sub, "D1.1", H5T.NATIVE_INT8, dataspaceId3);

        res = H5D.close(datasetId3);
        res = H5S.close(dataspaceId3);

        res = H5G.close(groupId);
        res = H5G.close(groupId_sub);
    }

    public static unsafe void AddMassLinks(long fileId)
    {
        var groupId = H5G.create(fileId, "mass_links");

        for (int i = 0; i < 1000; i++)
        {
            var linkId = H5G.create(groupId, $"mass_{i:D4}");
            _ = H5G.close(linkId);
        }

        _ = H5G.close(groupId);
    }

    public static unsafe void AddLinks(long fileId)
    {
        var groupId = H5G.create(fileId, "links");

        var hardLinkId1 = H5G.create(groupId, "hard_link_1");
        _ = H5L.create_hard(groupId, "hard_link_1", groupId, "hard_link_2");
        _ = H5L.create_soft("hard_link_2", groupId, "soft_link_1");
        _ = H5L.create_soft("/links/soft_link_1", groupId, "soft_link_2");

        var spaceId = H5S.create_simple(1, [1], [1]);
        var datasetId = H5D.create(hardLinkId1, "dataset", H5T.NATIVE_INT, spaceId);

        _ = H5L.create_soft("/links/soft_link_2/dataset", groupId, "dataset");

        _ = H5S.close(spaceId);
        _ = H5D.close(datasetId);

        _ = H5G.close(groupId);
        _ = H5G.close(hardLinkId1);
    }

    public static unsafe void AddExternalFileLink(long fileId, string externalFilePath)
    {
        var groupId = H5G.create(fileId, "links");
        _ = H5L.create_external(externalFilePath, "/external/group", groupId, "external_link");
        _ = H5G.close(groupId);
    }

    public static unsafe void AddCircularReference(long fileId)
    {
        var groupId_parent = H5G.create(fileId, "circular");

        _ = H5L.create_soft("/circular", groupId_parent, "soft");
        _ = H5L.create_hard(fileId, "circular", groupId_parent, "hard");

        var groupId_child1 = H5G.create(groupId_parent, "child");
        var groupId_child2 = H5G.create(groupId_child1, "rainbow's end");

        _ = H5G.close(groupId_child2);
        _ = H5G.close(groupId_child1);
        _ = H5G.close(groupId_parent);
    }
}