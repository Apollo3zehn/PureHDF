using PureHDF.Selections;
using PureHDF.VOL.Native;
using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Covers <see cref="NativeFile.GetAsync"/>, the asynchronous form of region reference resolution.
/// </summary>
/// <remarks>
/// A region reference names a SELECTION within another dataset, and resolving one reads the global heap
/// collection the reference points into - so it genuinely touches the file and the synchronous
/// <c>Get</c> could not serve one on a source that cannot be read synchronously.
/// <para>
/// The selection kinds mirror <c>DatasetTests.CanRead_Reference_Region</c>, including its exclusion: a
/// regular hyperslab appears to be impossible to create through the C library's reference API, so index
/// 2 is skipped there and here.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class AsyncRegionReferenceTests
{
    /// <summary>
    /// Every selection kind must resolve, and the data read through it must match the synchronous path.
    /// </summary>
    [Fact]
    public async Task GetAsyncResolvesEverySelectionKind()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            HDF.PInvoke.H5F.libver_t.LATEST,
            fileId => TestUtils.AddRegionReference(fileId, ContainerType.Dataset));

        using var stream = new PositionlessDatasetStream(File.ReadAllBytes(filePath), suspend: true);
        using var root = await H5File.OpenAsync(stream, leaveOpen: true);

        var group = await root.GroupAsync("reference");
        var referenced = await group.DatasetAsync("referenced");
        var region = await group.DatasetAsync("region");
        var references = await region.ReadAsync<NativeRegionReference1[]>();

        async Task<int[]> ReadRegion(NativeRegionReference1 reference)
        {
            var selection = await root.GetAsync(reference);

            return await referenced.ReadAsync<int[]>(fileSelection: selection);
        }

        // Act
        var actualNone = await ReadRegion(references[0]);
        var actualPoint = await ReadRegion(references[1]);
        var actualIrregularHyperslab = await ReadRegion(references[3]);
        var actualAll = await ReadRegion(references[4]);

        // Assert
        Assert.Empty(actualNone);
        Assert.Equal([2, 27, 59, 50], actualPoint);
        Assert.Equal([0, 1, 3, 4], actualIrregularHyperslab);
        Assert.Equal(SharedTestData.SmallData.Take(60), actualAll);
    }

    /// <summary>
    /// The asynchronous and synchronous forms must produce the same selection.
    /// </summary>
    /// <remarks>
    /// Compared through what the selection READS rather than by comparing Selection objects, because
    /// those are not value types across every kind and a reference comparison would pass vacuously.
    /// </remarks>
    [Fact]
    public async Task GetAsyncAgreesWithGet()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            HDF.PInvoke.H5F.libver_t.LATEST,
            fileId => TestUtils.AddRegionReference(fileId, ContainerType.Dataset));

        var fileBytes = File.ReadAllBytes(filePath);

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);

        var group = root.Group("reference");
        var referenced = group.Dataset("referenced");
        var references = group.Dataset("region").Read<NativeRegionReference1[]>();

        foreach (var index in new[] { 0, 1, 3, 4 })
        {
            // Act
            var syncSelection = root.Get(references[index]);
            var asyncSelection = await root.GetAsync(references[index]);

            // Assert
            Assert.Equal(
                referenced.Read<int[]>(fileSelection: syncSelection),
                referenced.Read<int[]>(fileSelection: asyncSelection));
        }
    }

    /// <summary>
    /// An invalid reference must be rejected the same way the synchronous form rejects it, and an
    /// already-cancelled token must prevent the work.
    /// </summary>
    [Fact]
    public async Task GetAsyncRejectsAnInvalidReferenceAndObservesCancellation()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            HDF.PInvoke.H5F.libver_t.LATEST,
            fileId => TestUtils.AddRegionReference(fileId, ContainerType.Dataset));

        using var stream = new PositionlessDatasetStream(File.ReadAllBytes(filePath), suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);

        // Act + Assert
        await Assert.ThrowsAsync<Exception>(() => root.GetAsync(default));

        var references = root.Group("reference").Dataset("region").Read<NativeRegionReference1[]>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => root.GetAsync(references[1], cts.Token));
    }
}
