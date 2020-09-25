using HDF.PInvoke;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests.Reading
{
    public class DatasetTests
    {
        private readonly ITestOutputHelper _logger;

        public DatasetTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        public static IList<object[]> DatasetNumericalTestData = TestData.DatasetNumericalTestData;

        [Theory]
        [MemberData(nameof(DatasetTests.DatasetNumericalTestData))]
        public void CanReadDataset_Numerical<T>(string name, T[] expected) where T : unmanaged
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.GetDataset($"/typed/{name}");
                var actual = dataset.Read<T>();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadDataset_NonNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.GetDataset("/typed/D14");
                var actual = dataset.Read<TestStructL1>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.NonNullableTestStructData));
            });
        }

        [Fact]
        public void CanReadDataset_NullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.GetDataset("/typed/D15");

                Func<FieldInfo, string> converter = fieldInfo =>
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute != null ? attribute.Name : fieldInfo.Name;
                };

                var actual = dataset.ReadCompound<TestStructString>(converter);

                // Assert
                Assert.True(actual.SequenceEqual(TestData.StringTestStructData));
            });
        }

        // Fixed-length string attribute (UTF8) is not supported because 
        // it is incompatible with variable byte length per character.
        [Theory]
        [InlineData("D11", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("D12", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("D13", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
        public void CanReadDataset_String(string name, string[] expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.GetDataset($"/typed/{name}");
                var actual = dataset.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void ThrowsForNestedNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.GetDataset($"/typed/D15");
                var exception = Assert.Throws<Exception>(() => dataset.ReadCompound<TestStructStringL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }

        [Fact]
        public void CanReadDataset_Compact()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddCompactDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("compact");
                var dataset = parent.GetDataset("compact");
                var actual = dataset.Read<byte>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.TinyData));
            });
        }

        [Fact]
        public void CanReadDataset_CompactTestFile()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = "testfiles/h5ex_d_compact.h5";
                var expected = new int[4, 7];

                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 7; j++)
                    {
                        expected[i, j] = i * j - j;
                    }
                }

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var parent = root;
                var dataset = parent.GetDataset("DS1");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(expected.Cast<int>()));
            });
        }

        [Fact]
        public void CanReadDataset_Contiguous()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddContiguousDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("contiguous");
                var dataset = parent.GetDataset("contiguous");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.HugeData));
            });
        }

        // https://support.hdfgroup.org/HDF5/doc_resource/H5Fill_Behavior.html
        // Fill value can only be inserted during read when data space is not allocated (late allocation).
        // As soon as the allocation happened, the fill value is either written or not written but during 
        // read this cannot be distinguished anymore. It is not possible to determine which parts of the
        // dataset have not been touched to insert a fill value in these buffers.
        [Fact]
        public void CanReadDataset_Contiguous_With_FillValue_And_AllocationLate()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var fillValue = 99;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddContiguousDatasetWithFillValueAndAllocationLate(fileId, fillValue));
            var expected = Enumerable.Range(0, TestData.MediumData.Length)
                .Select(value => fillValue)
                .ToArray();

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var group = root.GetGroup("fillvalue");
            var dataset = group.GetDataset($"{LayoutClass.Contiguous}");
            var actual = dataset.Read<int>();

            // Assert
            Assert.Equal(expected, actual);
        }

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
                    using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                    var parent = root.GetGroup("chunked");
                    var dataset = parent.GetDataset("chunked");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("chunked");
                var dataset = parent.GetDataset("chunked_single_chunk");
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
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.GetGroup("chunked");
            var dataset = parent.GetDataset("chunked_implicit");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("chunked");
                var dataset = parent.GetDataset("chunked_fixed_array");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("chunked");
                var dataset = parent.GetDataset("chunked_fixed_array_paged");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("chunked");
                var dataset = parent.GetDataset("chunked_extensible_array_elements");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("chunked");
                var dataset = parent.GetDataset("chunked_extensible_array_data_blocks");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("chunked");
                var dataset = parent.GetDataset("chunked_extensible_array_secondary_blocks");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("chunked");
                var dataset = parent.GetDataset("chunked_btree2");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.GetGroup("fillvalue");
                var dataset = group.GetDataset($"{LayoutClass.Chunked}");
                var actual = dataset.Read<int>();

                // Assert
                Assert.Equal(expected, actual);
            });
        }
    }
}