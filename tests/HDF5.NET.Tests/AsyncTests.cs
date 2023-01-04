using HDF.PInvoke;
using Xunit;

namespace HDF5.NET.Tests.Reading
{
    public class AsyncTests
    {
        [Fact]
        public async Task CanReadAttribute_Dataspace_Scalar()
        {
            // async version of test CanReadDataset_ChunkedBTree2
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_btree2");
                var actual = await dataset.ReadAsync<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }
    }
}