#if NET6_0_OR_GREATER

using HDF.PInvoke;
using Xunit;

namespace HDF5.NET.Tests.Reading
{
    public class AsyncTests
    {
        [Fact]
        public async Task CanReadDataset_ChunkedBTree2()
        {
            // async version of test CanReadDataset_ChunkedBTree2
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle));

                // Act
                using var root = H5File.OpenCore(
                    filePath,
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.Read, 
                    useAsync: true, 
                    deleteOnClose: true);

                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_btree2");
                var actual = await dataset.ReadAsync<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public async Task CanReadDatasetParallel()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Huge(fileId));
            var tasks = new List<Task>();

            // Act
            using var root = H5File.OpenCore(
                filePath,
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                useAsync: true, 
                deleteOnClose: true);

            var parent = root.Group("chunked");
            var dataset = parent.Dataset("chunked_huge");

            const int chunkSize = 1_000_000;

            for (int i = 0; i < 10; i++)
            {
                var fileSelection = new HyperslabSelection(
                    start: (uint)i * chunkSize,
                    block: chunkSize
                );

                var localIndex = i;

                var task = Task.Run(async () =>
                {
                    var actual = await dataset.ReadAsync<int>(fileSelection);

                    // Assert
                    var slicedData = TestData.HugeData.AsSpan(localIndex * chunkSize, chunkSize).ToArray();
                    Assert.True(actual.SequenceEqual(slicedData));
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }
    }
}

#endif