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

        public static IList<object[]> DatasetNumericalTestData = TestData.DatasetNumericalData;

        [Theory]
        [MemberData(nameof(DatasetTests.DatasetNumericalTestData))]
        public void CanReadDataset_Numerical<T>(string name, T[] expected) where T : struct
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddNumericalDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Dataset($"/numerical/{name}");
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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStructDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nonnullable");
                var actual = dataset.Read<TestStructL1>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.NonNullableStructData));
            });
        }

        [Fact]
        public void CanReadDataset_NullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStructDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");

                Func<FieldInfo, string> converter = fieldInfo =>
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute != null ? attribute.Name : fieldInfo.Name;
                };

                var actual = dataset.ReadCompound<TestStructString>(converter);

                // Assert
                Assert.True(actual.SequenceEqual(TestData.StringStructData));
            });
        }

        // Fixed-length string dataset (UTF8) is not supported because 
        // it is incompatible with variable byte length per character.
        [Theory]
        [InlineData("fixed", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("variable", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("variableUTF8", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
        public void CanReadDataset_String(string name, string[] expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStringDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Dataset($"/string/{name}");
                var actual = dataset.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadDataset_Bitfield()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddBitFieldDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Group("bitfield").Dataset("bitfield");
                var actual = dataset.Read<TestBitfield>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.BitfieldData));
            });
        }

        [Fact]
        public void CanReadDataset_Opaque()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddOpaqueDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Group("opaque").Dataset("opaque");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.SmallData));
            });
        }

        [Fact]
        public void CanReadDataset_Array()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddArrayDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Group("array").Dataset("array");
                var actual = dataset
                    .Read<int>()
                    .ToArray4D(2, 3, 4, 5);

                var expected_casted = TestData.ArrayData.Cast<int>().ToArray();
                var actual_casted = actual.Cast<int>().ToArray();

                // Assert
                Assert.True(actual_casted.SequenceEqual(expected_casted));
            });
        }

        [Fact]
        public void CanReadDataset_Reference()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddReferenceDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset_references = root.Group("reference").Dataset("reference");
                var references = dataset_references.Read<ulong>();

                var dereferenced = references
                    .Select(references => root.Get(references))
                    .ToArray();

                // Assert
                for (int i = 0; i < TestData.DatasetNumericalData.Count; i++)
                {
                    var dataset = (H5Dataset)dereferenced[i];
                    var expected = (Array)TestData.DatasetNumericalData[i][1];
                    var elementType = expected.GetType().GetElementType();

                    var method = typeof(TestUtils).GetMethod(nameof(TestUtils.ReadAndCompare), BindingFlags.Public | BindingFlags.Static);
                    var generic = method.MakeGenericMethod(elementType);
                    var result = (bool)generic.Invoke(null, new object[] { dataset, expected });

                    Assert.True(result);
                }
            });
        }

        [Fact]
        public void ThrowsForNestedNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStructDatasets(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Dataset($"/struct/nullable");
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
                var parent = root.Group("compact");
                var dataset = parent.Dataset("compact");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.SmallData));
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
                var dataset = parent.Dataset("DS1");
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
                var parent = root.Group("contiguous");
                var dataset = parent.Dataset("contiguous");
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
            var group = root.Group("fillvalue");
            var dataset = group.Dataset($"{LayoutClass.Contiguous}");
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
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
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.Group("fillvalue");
                var dataset = group.Dataset($"{LayoutClass.Chunked}");
                var actual = dataset.Read<int>();

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Fact]
        public void CanReadBigEndian()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            {
                TestUtils.AddSmallAttribute(fileId);
                TestUtils.AddCompactDataset(fileId);
                TestUtils.AddContiguousDataset(fileId);
                TestUtils.AddChunkedDataset_Single_Chunk(fileId, withShuffle: false);
            });

            /* modify file to declare datasets and attributes layout as big-endian */
            using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)))
            {
                reader.BaseStream.Seek(0x121, SeekOrigin.Begin);
                var data1 = reader.ReadByte();
                writer.BaseStream.Seek(0x121, SeekOrigin.Begin);
                writer.Write((byte)(data1 | 0x01));

                reader.BaseStream.Seek(0x39C, SeekOrigin.Begin);
                var data2 = reader.ReadByte();
                writer.BaseStream.Seek(0x39C, SeekOrigin.Begin);
                writer.Write((byte)(data2 | 0x01));

                reader.BaseStream.Seek(0x6DB, SeekOrigin.Begin);
                var data3 = reader.ReadByte();
                writer.BaseStream.Seek(0x6DB, SeekOrigin.Begin);
                writer.Write((byte)(data3 | 0x01));

                reader.BaseStream.Seek(0x89A, SeekOrigin.Begin);
                var data4 = reader.ReadByte();
                writer.BaseStream.Seek(0x89A, SeekOrigin.Begin);
                writer.Write((byte)(data4 | 0x01));
            };

            /* continue */
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);

            var attribute = root
                .Group("small")
                .Attribute("small");

            var dataset_compact = root
                .Group("compact")
                .Dataset("compact");

            var dataset_contiguous = root
                .Group("contiguous")
                .Dataset("contiguous");

            var dataset_chunked = root
                .Group("chunked")
                .Dataset("chunked_single_chunk");

            var attribute_expected = new int[TestData.SmallData.Length];
            EndiannessConverter.Convert<int>(TestData.SmallData, attribute_expected);

            var dataset_compact_expected = new int[TestData.SmallData.Length];
            EndiannessConverter.Convert<int>(TestData.SmallData, dataset_compact_expected);

            var dataset_contiguous_expected = new int[TestData.HugeData.Length];
            EndiannessConverter.Convert<int>(TestData.HugeData, dataset_contiguous_expected);

            var dataset_chunked_expected = new int[TestData.MediumData.Length];
            EndiannessConverter.Convert<int>(TestData.MediumData, dataset_chunked_expected);

            // Act
            var attribute_actual = attribute.Read<int>();
            var dataset_compact_actual = dataset_compact.Read<int>();
            var dataset_contiguous_actual = dataset_contiguous.Read<int>();
            var dataset_chunked_actual = dataset_chunked.Read<int>();

            // Assert
            Assert.True(dataset_compact_actual.SequenceEqual(dataset_compact_expected));
            Assert.True(dataset_contiguous_actual.SequenceEqual(dataset_contiguous_expected));
            Assert.True(dataset_chunked_actual.SequenceEqual(dataset_chunked_expected));
        }
    }
}