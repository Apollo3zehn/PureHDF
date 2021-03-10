using HDF.PInvoke;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

        public static IList<object[]> DatasetNumericalTestData = TestData.NumericalData;

        [Fact]
        public void CanReadDataset_Dataspace_Scalar()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataspaceScalar(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.Group("dataspace").Dataset("scalar");
                var actual = attribute.Read<double>();

                // Assert
                Assert.True(actual.SequenceEqual(new double[] { -1.2234234e-3 }));
            });
        }

        [Fact]
        public void CanReadDataset_Dataspace_Null()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataspaceNull(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.Group("dataspace").Dataset("null");
                var actual = attribute.Read<double>();

                // Assert
                Assert.True(actual.Length == 0);
            });
        }

        [Theory]
        [MemberData(nameof(DatasetTests.DatasetNumericalTestData))]
        public void CanReadDataset_Numerical<T>(string name, T[] expected) where T : unmanaged
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddNumerical((long)fileId, ContainerType.Dataset)));

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
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddStruct((long)fileId, ContainerType.Dataset)));

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
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddStruct((long)fileId, ContainerType.Dataset)));

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
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddString((long)fileId, ContainerType.Dataset)));

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
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddBitField((long)fileId, ContainerType.Dataset)));

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
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddOpaque((long)fileId, ContainerType.Dataset)));

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
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddArray((long)fileId, ContainerType.Dataset)));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Group("array").Dataset("array");
                var actual = dataset
                    .Read<int>()
                    .ToArray4D(2, 3, 4, 5);

                var expected_casted = TestData.ArrayData.Cast<int>().ToArray();
                var actual_casted = actual.Cast<int>().ToArray();

                var b = MemoryMarshal.AsBytes(expected_casted.AsSpan());
                var c = MemoryMarshal.AsBytes(actual_casted.AsSpan());

                // Assert
                Assert.True(actual_casted.SequenceEqual(expected_casted));
            });
        }

        [Fact]
        public void CanReadDataset_Reference_Object()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddObjectReference(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset_references = root.Group("reference").Dataset("object_reference");
                var references = dataset_references.Read<H5ObjectReference>();

                var dereferenced = references
                    .Select(reference => root.Get(reference))
                    .ToArray();

                // Assert
                for (int i = 0; i < TestData.NumericalData.Count; i++)
                {
                    var dataset = (H5Dataset)dereferenced[i];
                    var expected = (Array)TestData.NumericalData[i][1];
                    var elementType = expected.GetType().GetElementType();

                    var method = typeof(TestUtils).GetMethod(nameof(TestUtils.ReadAndCompare), BindingFlags.Public | BindingFlags.Static);
                    var generic = method.MakeGenericMethod(elementType);
                    var result = (bool)generic.Invoke(null, new object[] { dataset, expected });

                    Assert.True(result);
                }
            });
        }

        [Fact(Skip = "Not yet fully implemented.")]
        public void CanReadDataset_Reference_Region()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddRegionReference(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset_references = root.Group("reference").Dataset("region_reference");
                var references = dataset_references.Read<H5RegionReference>();

                var reference = references[0];
                root.Context.Reader.Seek((long)reference.CollectionAddress, SeekOrigin.Begin);

                // H5Rint.c (H5R__get_region)
#warning use more structs?
                var globalHeapId = new GlobalHeapId(root.Context.Superblock)
                {
                    CollectionAddress = reference.CollectionAddress,
                    ObjectIndex = reference.ObjectIndex
                };
                
                var globalHeapCollection = globalHeapId.Collection;
                var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex - 1];
                var localReader = new H5BinaryReader(new MemoryStream(globalHeapObject.ObjectData));
                var address = root.Context.Superblock.ReadOffset(localReader);
                var selection = new DataspaceSelection(localReader);

                throw new NotImplementedException();
            });
        }

        [Fact]
        public void ThrowsForNestedNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddStruct((long)fileId, ContainerType.Dataset)));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var dataset = root.Dataset($"/struct/nullable");
                var exception = Assert.Throws<Exception>(() => dataset.ReadCompound<TestStructStringL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }

        [Theory]
        [InlineData("absolute")]
        [InlineData("relative")]
        [InlineData("prefix")]
        public void CanReadDataset_External(string datasetName)
        {
            // WARNING:
            // It seems that there is a bug in the native HDF library. In this test, sometimes the fill value
            // is defined with a value of 99, which must come from test 'CanReadDataset_Contiguous_With_FillValue_And_AllocationLate'.

            // INFO:
            // HDF lib says "external storage not supported with chunked layout". Same is true for compact layout.
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var absolutePrefix = datasetName == "absolute" 
                    ? Path.GetTempPath() 
                    : string.Empty;

                var externalFilePrefix = datasetName == "prefix"
                   ? Path.GetTempPath()
                   : null;

                var datasetAccess = new H5DatasetAccess()
                {
                    ExternalFilePrefix = externalFilePrefix
                };

                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddExternalDataset(fileId, datasetName, absolutePrefix, datasetAccess));
                var expected = TestData.MediumData;

                for (int i = 33; i < 40; i++)
                {
                    expected[i] = 0;
                }

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Group("external");
                var dataset = parent.Dataset(datasetName);
                var actual = dataset.Read<int>();

                // Assert
                _logger.WriteLine(actual[1000].ToString());
                _logger.WriteLine(expected[1000].ToString());

                for (int i = 0; i < actual.Length; i++)
                {
                    if (actual[i] != expected[i])
                    {
                        _logger.WriteLine("index_" + i.ToString());
                        _logger.WriteLine(actual[i].ToString());
                        _logger.WriteLine(expected[i].ToString());
                    }
                }

                Assert.True(actual.SequenceEqual(expected));
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
    }
}