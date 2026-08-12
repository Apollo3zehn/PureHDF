using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// A virtual dataset whose sources live in the same file, i.e. a source file name of ".".
/// </summary>
/// <remarks>
/// This is the only form of virtual source that can work over a non-filesystem source.
/// <c>FilePathUtils.FindExternalFileForVirtualDataset</c> returns "." immediately for it, before any
/// <c>File.Exists</c> probing, and the caller then reuses the already-open file rather than opening
/// one by path.
/// <para>
/// An EXTERNAL source cannot: resolution probes candidate paths on the local filesystem and opens the
/// result by path, and the first candidate is built from <c>_file.FolderPath</c>, which is empty for a
/// stream-opened file. Worth separating from the same-file case explicitly, because the existing
/// <c>AddVirtualDataset</c> fixture uses external sources written as bare relative paths into the
/// process working directory - so it resolves them off the filesystem and says nothing about streams.
/// </para>
/// </remarks>
public class VirtualDatasetSameFileTests
{
    private static readonly int[] _expected =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1];

    [Fact]
    public void CanReadAVirtualDatasetWhoseSourceIsInTheSameFile()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.V110,
            fileId => TestUtils.AddVirtualDatasetSameFile(fileId, "vds_same_file"));

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

        // Act
        var actual = root.Dataset("vds_same_file").Read<int[]>();

        // Assert
        Assert.Equal<int[]>(_expected, actual);
    }

    /// <summary>
    /// The same read through a stream that suspends on every read and has no filesystem behind it.
    /// </summary>
    /// <remarks>
    /// This is the case the WASM viewer depends on. It proves two things at once: that the gather is
    /// genuinely asynchronous end to end, and that resolving a "." source never reaches for a file
    /// path - the stream is fed from a byte array, so any filesystem probing would fail and the whole
    /// result would come back as the fill value.
    /// </remarks>
    [Fact]
    public async Task CanReadASameFileVirtualDatasetThroughASuspendingStream()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.V110,
            fileId => TestUtils.AddVirtualDatasetSameFile(fileId, "vds_same_file"));

        var bytes = File.ReadAllBytes(filePath);
        File.Delete(filePath);

        using var stream = new PositionlessDatasetStream(bytes, suspend: true);
        using var root = await H5File.OpenAsync(stream, leaveOpen: true);
        var dataset = await root.DatasetAsync("vds_same_file");

        // Act
        var actual = await dataset.ReadAsync<int[]>();

        // Assert
        Assert.Equal<int[]>(_expected, actual);
    }
}
