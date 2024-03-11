#if NET6_0_OR_GREATER

using System.IO.MemoryMappedFiles;
using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading;

public class ConcurrencyTests
{
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

        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        const int CHUNK_SIZE = 1_000_000;

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

        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        const int CHUNK_SIZE = 1_000_000;

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
}

#endif