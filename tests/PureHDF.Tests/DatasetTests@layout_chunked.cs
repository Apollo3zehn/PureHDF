using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public partial class DatasetTests
    {
        [Fact]
        public void CanReadDataset_Chunked_Legacy()
        {
            var versions = new H5F.libver_t[]
            {
                H5F.libver_t.EARLIEST,
                H5F.libver_t.V18
            };

            TestUtils.RunForVersions(versions, version =>
            {
                foreach (var withShuffle in new bool[] { false, true })
                {
                    // Arrange
                    var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Legacy(fileId, withShuffle));

                    // Act
                    using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                    var parent = root.Group("chunked");
                    var dataset = parent.Dataset("chunked");
                    var actual = dataset.Read<int>();

                    // Assert
                    Assert.True(actual.SequenceEqual(TestData.MediumData));
                }
            });
        }

        [Fact]
        public void CanReadDataset_ChunkedSingleChunk()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Single_Chunk(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_single_chunk");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public void CanReadDataset_ChunkedImplicit()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Implicit(fileId));

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("chunked");
            var dataset = parent.Dataset("chunked_implicit");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestData.MediumData));
        }

        [Fact]
        public void CanReadDataset_ChunkedFixedArray()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Fixed_Array(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_fixed_array");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public void CanReadDataset_ChunkedFixedArrayPaged()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Fixed_Array_Paged(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_fixed_array_paged");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public void CanReadDataset_ChunkedExtensibleArrayElements()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Extensible_Array_Elements(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_extensible_array_elements");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public void CanReadDataset_ChunkedExtensibleArrayDataBlocks()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Extensible_Array_Data_Blocks(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_extensible_array_data_blocks");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public void CanReadDataset_ChunkedExtensibleArraySecondaryBlocks()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Extensible_Array_Secondary_Blocks(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_extensible_array_secondary_blocks");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public void CanReadDataset_ChunkedBTree2()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("chunked");
                var dataset = parent.Dataset("chunked_btree2");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.MediumData));
            }
        }

        [Fact]
        public void CanReadDataset_Chunked_With_FillValue_And_AllocationLate()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var fillValue = 99;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDatasetWithFillValueAndAllocationLate(fileId, fillValue));
                var expected = Enumerable.Range(0, TestData.MediumData.Length)
                    .Select(value => fillValue)
                    .ToArray();

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var group = root.Group("fillvalue");
                var dataset = group.Dataset($"{LayoutClass.Chunked}");
                var actual = dataset.Read<int>();

                // Assert
                Assert.Equal(expected, actual);
            });
        }
    }
}