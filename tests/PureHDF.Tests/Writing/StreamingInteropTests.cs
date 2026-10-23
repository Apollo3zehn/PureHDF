using System.Diagnostics;
using System.Runtime.InteropServices;
using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
/// A streamed file is a file the HDF5 C library reads and changes like any other: it sees the link counts
/// of shared objects, keeps an object alive while a link to it is left, adds to groups created by an
/// append, and copies the whole file.
/// </summary>
[Collection(SharedHdf5StateCollection.Name)]
public class StreamingInteropTests
{
    private static readonly int[] Values = [1, 2, 3, 4];

    [Fact]
    public void TheCLibrarySeesTheLinkCountsOfSharedObjects()
    {
        WithStreamedFile(filePath =>
        {
            var fileId = H5F.open(filePath, H5F.ACC_RDONLY);
            Assert.True(fileId >= 0);

            try
            {
                Assert.Equal(3U, LinkCount(fileId, "/shared"));
                Assert.Equal(2U, LinkCount(fileId, "/declared"));
                Assert.Equal(1U, LinkCount(fileId, "/documents/0/values"));
                Assert.Equal(Values, ReadInts(fileId, "/documents/1/shared"));
                Assert.Equal(Values, ReadInts(fileId, "/documents/0/deferred"));
            }

            finally
            {
                _ = H5F.close(fileId);
            }
        });
    }

    [Fact]
    public void TheCLibraryCanUnlinkAndAddToAStreamedFile()
    {
        WithStreamedFile(filePath =>
        {
            // Act
            var fileId = H5F.open(filePath, H5F.ACC_RDWR);
            Assert.True(fileId >= 0);

            try
            {
                Assert.True(H5L.delete(fileId, "/shared") >= 0);

                var space = H5S.create_simple(1, [4], [4]);
                var datasetId = H5D.create(fileId, "/documents/added", H5T.NATIVE_INT32, space);
                Assert.True(datasetId >= 0, "the C library could not add to a group created by an append");

                _ = H5D.close(datasetId);
                _ = H5S.close(space);

                Assert.Equal(2U, LinkCount(fileId, "/documents/0/shared"));
            }

            finally
            {
                _ = H5F.close(fileId);
            }

            // Assert - the shared dataset outlives the link that was removed
            using var file = H5File.OpenRead(filePath);

            Assert.False(file.LinkExists("shared"));
            Assert.True(file.LinkExists("documents/added"));
            Assert.Equal(Values, file.Dataset("documents/1/shared").Read<int[]>());
        });
    }

    [Fact]
    public void TheCLibraryCanCopyAStreamedFile()
    {
        WithStreamedFile(filePath =>
        {
            var copyPath = filePath + ".repacked";

            try
            {
                // Act
                var exitCode = RunTool("h5repack", $"\"{filePath}\" \"{copyPath}\"");

                if (exitCode is null)
                    return;

                // Assert
                Assert.Equal(0, exitCode);

                using var copy = H5File.OpenRead(copyPath);

                Assert.Equal(Values, copy.Dataset("documents/1/shared").Read<int[]>());
                Assert.Equal(Values, copy.Dataset("documents/0/deferred").Read<int[]>());
                Assert.Equal("streamed", copy.Attribute("title").Read<string>());
            }

            finally
            {
                File.Delete(copyPath);
            }
        });
    }

    private static void WithStreamedFile(Action<string> action)
    {
        var filePath = Path.GetTempFileName();

        try
        {
            var shared = new H5Dataset(Values) { IsShared = true };
            var declared = new H5Dataset(Values) { HardLinkCount = 2 };
            var deferred = new H5Dataset<int[]>(fileDims: [4]);

            using (var writer = H5File.BeginStreaming(filePath))
            {
                writer.Root.Attributes["title"] = "streamed";

                writer.Append("shared", shared);
                writer.Append("declared", declared);
                writer.Append("documents/0", new H5Group { ["values"] = Values, ["shared"] = shared, ["deferred"] = deferred });
                writer.Append("documents/1", new H5Group { ["shared"] = shared, ["declared"] = declared });
                writer.Write(deferred, Values);
            }

            action(filePath);
        }

        finally
        {
            File.Delete(filePath);
        }
    }

    private static uint LinkCount(long fileId, string path)
    {
        var info = default(H5O.info_t);
        Assert.True(H5O.get_info_by_name(fileId, path, ref info) >= 0, $"the C library could not stat '{path}'");

        return info.rc;
    }

    private static int[] ReadInts(long fileId, string path)
    {
        var datasetId = H5D.open(fileId, path);
        Assert.True(datasetId >= 0, $"the C library could not open '{path}'");

        try
        {
            var buffer = new int[4];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                Assert.True(H5D.read(datasetId, H5T.NATIVE_INT32, H5S.ALL, H5S.ALL, H5P.DEFAULT, handle.AddrOfPinnedObject()) >= 0);
            }

            finally
            {
                handle.Free();
            }

            return buffer;
        }

        finally
        {
            _ = H5D.close(datasetId);
        }
    }

    // Null when the tool is not installed.
    private static int? RunTool(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        try
        {
            process.Start();
        }

        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} did not exit within 60 s.");
        }

        Assert.True(process.ExitCode == 0, $"{fileName} failed: {stderr.Result}{stdout.Result}");

        return process.ExitCode;
    }
}
