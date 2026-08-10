using System.IO.MemoryMappedFiles;
using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading;

// CONCURRENCY MODEL: a driver instance is owned by one logical reader (its cursor is a plain field,
// because a ThreadLocal<long> reads back as 0 once an async continuation resumes on another thread).
// Concurrency is therefore provided per *read operation*, not per reader: NativeDataset.Read and
// NativeAttribute.Read each allocate a driver over the same file handle / memory-mapped accessor for
// the duration of the operation. So a single H5File can serve many threads at once.
//
// BOUNDARY - what these tests deliberately do NOT do concurrently: object navigation. root.Group(),
// root.Dataset(), attribute enumeration and anything else that walks the file structure moves the
// FILE-LEVEL driver cursor and has no per-operation driver of its own. That is why every test below
// resolves the dataset ONCE, on the calling thread, and only the Read calls run in parallel. Moving
// a `.Dataset(...)` call inside a Parallel.For here would be testing an unsupported usage.
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
        using var root = NativeFile.InternalOpen(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            deleteOnClose: true);

        // resolved once, on this thread - see the boundary note above
        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        Parallel.For(0, 10, i =>
        {
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

    [Fact]
    public void CanReadDatasetParallel_MMF_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);

        // Act
        using var mmf = MemoryMappedFile.CreateFromFile(filePath);
        using var accessor = mmf.CreateViewAccessor();
        using var root = H5File.Open(accessor);

        // resolved once, on this thread - see the boundary note above
        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        Parallel.For(0, 10, i =>
        {
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

    // Variable-length data is the case the fixed-size tests above cannot reach, and the one that was
    // silently racy even before the async conversion: the element bytes hold a global-heap ID, so the
    // decoder calls NativeCache.GetGlobalHeapObject, which SEEKS AND READS the driver in the middle
    // of the dataset read (saving and restoring the cursor around it) and populates a process-wide
    // cache. Sharing one driver across threads corrupts both the cursor and the collection decode.
    //
    // Every thread starts on a cold cache, so they collide on the first-miss path, and the assertion
    // is on the decoded strings - a race here yields wrong or truncated values, not an exception.
    [Theory]
    [InlineData(DriverKind.FileHandle)]
    [InlineData(DriverKind.MemoryMappedFile)]
    public void CanReadVariableLengthDatasetParallel_Threads(DriverKind driverKind)
    {
        // Arrange
        var version = H5F.libver_t.LATEST;

        var filePath = TestUtils.PrepareTestFile(version, fileId
            => TestUtils.AddString(fileId, ContainerType.Dataset));

        var expected = new string[]
        {
            "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!"
        };

        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? accessor = null;
        NativeFile root;

        if (driverKind == DriverKind.MemoryMappedFile)
        {
            mmf = MemoryMappedFile.CreateFromFile(filePath);
            accessor = mmf.CreateViewAccessor();
            root = H5File.Open(accessor);
        }

        else
        {
            root = NativeFile.InternalOpen(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                deleteOnClose: false);
        }

        try
        {
            // resolved once, on this thread - see the boundary note above
            var dataset = root.Group("string").Dataset("variable");

            // Act
            Parallel.For(0, 64, _ =>
            {
                var actual = dataset.Read<string[]>();

                // Assert
                Assert.Equal(expected.Length, actual.Length);

                for (int j = 0; j < expected.Length; j++)
                {
                    Assert.Equal(expected[j], actual[j]);
                }
            });
        }

        finally
        {
            root.Dispose();
            accessor?.Dispose();
            mmf?.Dispose();

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    public enum DriverKind
    {
        FileHandle,
        MemoryMappedFile
    }
}
