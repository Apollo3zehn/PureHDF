using System.IO.MemoryMappedFiles;
using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading;

// CONCURRENCY MODEL (async-first): isolation moved from "one shared driver whose cursor lives in a
// ThreadLocal<long>" to "one reader per concurrent consumer". The old model cannot survive async -
// a continuation may resume on a different thread, where the ThreadLocal cursor reads back as 0 and
// the read silently targets the wrong offset.
//
// So these tests now open a reader inside each parallel iteration rather than sharing one across
// threads. Parallel reading is still supported; what changed is who owns the cursor. Note this also
// removes the reliance on SimpleReadingChunkCache being thread-safe, which it never was (it mutates
// a plain Dictionary with no synchronization).
[Collection(SharedHdf5StateCollection.Name)]
public class ConcurrencyTests
{
    private const int CHUNK_SIZE = 1_000_000;

    [Fact]
    public void CanReadDatasetParallel_File_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);

        // Act
        Parallel.For(0, 10, i =>
        {
            // one reader per iteration - see the note above
            using var root = NativeFile.InternalOpen(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                deleteOnClose: false);

            var parent = root.Group("chunked");
            var dataset = parent.Dataset("chunked_huge");

            var fileSelection = new HyperslabSelection(
                start: (uint)i * CHUNK_SIZE,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(fileSelection);

            // Assert
            var slicedData = SharedTestData.HugeData.AsSpan(i * CHUNK_SIZE, CHUNK_SIZE).ToArray();
            Assert.True(actual.SequenceEqual(slicedData));
        });

        File.Delete(filePath);
    }

    [Fact]
    public void CanReadDatasetParallel_MMF_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);

        // Act
        // The memory-mapped file itself is safe to share; the per-reader state is the accessor and
        // the driver built over it, so both are created inside the loop.
        using var mmf = MemoryMappedFile.CreateFromFile(filePath);

        Parallel.For(0, 10, i =>
        {
            using var accessor = mmf.CreateViewAccessor();
            using var root = H5File.Open(accessor);

            var parent = root.Group("chunked");
            var dataset = parent.Dataset("chunked_huge");

            var fileSelection = new HyperslabSelection(
                start: (uint)i * CHUNK_SIZE,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(fileSelection);

            // Assert
            var slicedData = SharedTestData.HugeData.AsSpan(i * CHUNK_SIZE, CHUNK_SIZE).ToArray();
            Assert.True(actual.SequenceEqual(slicedData));
        });
    }
}
