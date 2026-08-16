using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
/// A group this library writes can have a named object added to it by the HDF5 C library.
/// </summary>
/// <remarks>
/// Every link insertion goes through <c>H5G_obj_insert()</c>, which reads the group info message to learn
/// the link phase change thresholds. Without that message the C library fails with "message type not
/// found" before writing anything useful - so a file was readable by every tool and extendable by none,
/// for datasets exactly as much as for groups.
/// <para>
/// These tests drive the real C library rather than asserting the message is present, because the
/// message being present is not the property that matters - being able to insert is.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class GroupInfoMessageTests
{
    /// <summary>
    /// Writes <paramref name="file"/>, reopens it with the C library, and runs <paramref name="insert"/>
    /// against the root group. Returns the C library's status.
    /// </summary>
    private static long InsertWithTheCLibrary(H5File file, Func<long, long> insert)
    {
        var filePath = Path.GetTempFileName();

        try
        {
            file.Write(filePath);

            var fileId = H5F.open(filePath, H5F.ACC_RDWR);
            Assert.True(fileId >= 0, "the C library could not open the written file for writing");

            try
            {
                var objectId = insert(fileId);

                if (objectId >= 0)
                    _ = H5O.close(objectId);

                return objectId;
            }
            finally
            {
                _ = H5F.close(fileId);
            }
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ACLibraryCanAddAGroup()
    {
        var file = new H5File { ["existing"] = new H5Group() };

        var status = InsertWithTheCLibrary(file, fileId => H5G.create(fileId, "added"));

        Assert.True(status >= 0, "the C library could not create a group in the written file");
    }

    [Fact]
    public void ACLibraryCanAddADataset()
    {
        var file = new H5File { ["existing"] = new H5Group() };

        var status = InsertWithTheCLibrary(file, fileId =>
        {
            var space = H5S.create_simple(1, [4], [4]);

            try
            {
                return H5D.create(fileId, "added", H5T.NATIVE_INT32, space);
            }
            finally
            {
                _ = H5S.close(space);
            }
        });

        Assert.True(status >= 0, "the C library could not create a dataset in the written file");
    }

    /// <summary>
    /// Above the default threshold the C library converts the group's links from compact storage to a
    /// fractal heap, which is a different and much larger code path than a plain insert.
    /// </summary>
    [Fact]
    public void ACLibraryCanAddToAGroupAlreadyPastTheCompactThreshold()
    {
        var file = new H5File();

        // The C library's default maximum compact value is 8.
        for (var i = 0; i < 20; i++)
        {
            file[$"existing{i:D2}"] = new H5Group();
        }

        var status = InsertWithTheCLibrary(file, fileId => H5G.create(fileId, "added"));

        Assert.True(status >= 0, "the C library could not insert into a group past the compact threshold");
    }

    /// <summary>
    /// The inserted object must actually be there once the C library closes the file, and the file must
    /// still be readable by this library afterwards.
    /// </summary>
    [Fact]
    public void AnInsertedGroupSurvivesAndTheFileStaysReadable()
    {
        var filePath = Path.GetTempFileName();

        try
        {
            new H5File { ["existing"] = new H5Group() }.Write(filePath);

            var fileId = H5F.open(filePath, H5F.ACC_RDWR);
            Assert.True(fileId >= 0);

            var groupId = H5G.create(fileId, "added");
            Assert.True(groupId >= 0);
            _ = H5G.close(groupId);
            _ = H5F.close(fileId);

            using var reopened = H5File.OpenRead(filePath);

            Assert.True(reopened.LinkExists("existing"), "the original group is gone");
            Assert.True(reopened.LinkExists("added"), "the group the C library added is not there");
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
