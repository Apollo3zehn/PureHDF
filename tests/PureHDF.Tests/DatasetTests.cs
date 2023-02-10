using HDF.PInvoke;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public partial class DatasetTests
    {
        private readonly JsonSerializerOptions _options = new() 
        { 
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static IList<object[]> DatasetNumericalTestData { get; } = TestData.NumericalData;

        [Fact]
        public void CanReadDataset_Dataspace_Scalar()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataspaceScalar(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("dataspace").Dataset("null");
                var actual = attribute.Read<double>();

                // Assert
                Assert.True(actual.Length == 0);
            });
        }

        [Theory]
        [MemberData(nameof(DatasetNumericalTestData))]
        public void CanReadDataset_Numerical<T>(string name, T[] expected) where T : unmanaged
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddNumerical(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");

                static string converter(FieldInfo fieldInfo)
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : fieldInfo.Name;
                }

                var actual = dataset.ReadCompound<TestStructStringAndArray>(converter);

                // Assert
                Assert.Equal(
                    JsonSerializer.Serialize(TestData.StringStructData, _options), 
                    JsonSerializer.Serialize(actual, _options));
            });
        }

        [Fact]
        public void CanReadDataset_NullableStruct_memory_selection()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");
                var memorySelection = new HyperslabSelection(start: 1, stride: 2, count: 12, block: 1);

                static string converter(FieldInfo fieldInfo)
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : fieldInfo.Name;
                }

                var actual = dataset.ReadCompound<TestStructStringAndArray>(
                    converter,
                    memorySelection: memorySelection, 
                    memoryDims: new ulong[] { 24 });

                // Assert
                Assert.Equal(24, actual.Length);

                var defaultSerialized = JsonSerializer.Serialize(default(TestStructStringAndArray), _options);

                defaultSerialized = defaultSerialized
                    .Replace("\"FloatArray\":null", "\"FloatArray\":[0,0,0]");

                for (int i = 0; i < actual.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        Assert.Equal(
                            defaultSerialized, 
                            JsonSerializer.Serialize(actual[i], _options));
                    }

                    else
                    {
                        Assert.Equal(
                            JsonSerializer.Serialize(TestData.StringStructData[i / 2], _options), 
                            JsonSerializer.Serialize(actual[i], _options));
                    }
                }
            });
        }

        [Fact]
        public void CanReadDataset_UnknownStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");
                var actual = dataset.ReadCompound();

                // Assert
                Assert.Equal(
                    JsonSerializer.Serialize(TestData.StringStructData, _options).Replace("ShortValueWithCustomName", "ShortValue"),
                    JsonSerializer.Serialize(actual, _options));
            });
        }

        [Fact]
        public void CanReadDataset_UnknownStruct_memory_selection()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");
                var memorySelection = new HyperslabSelection(start: 1, stride: 2, count: 12, block: 1);

                var actual = dataset.ReadCompound(
                    memorySelection: memorySelection, 
                    memoryDims: new ulong[] { 24 });

                // Assert
                Assert.Equal(24, actual.Length);

                var defaultSerialized = JsonSerializer
                .Serialize(default(TestStructStringAndArray), _options);

                defaultSerialized = defaultSerialized
                    .Replace("\"FloatArray\":null", "\"FloatArray\":[0,0,0]")
                    .Replace("ShortValueWithCustomName", "ShortValue");
                    
                for (int i = 0; i < actual.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        Assert.Equal(
                            defaultSerialized, 
                            JsonSerializer.Serialize(actual[i], _options));
                    }

                    else
                    {
                        Assert.Equal(
                            JsonSerializer
                                .Serialize(TestData.StringStructData[i / 2], _options)
                                .Replace("ShortValueWithCustomName", "ShortValue"), 
                            JsonSerializer
                                .Serialize(actual[i], _options));
                    }
                }
            });
        }

        // Fixed-length string dataset (UTF8) is not supported because 
        // it is incompatible with variable byte length per character.
        [Theory]
        [InlineData("fixed+nullterm", new string[] { "00", "11", "22", "3", "44 ", "555", "66 ", "77", "  ", "AA ", "ZZ ", "!!" })]
        [InlineData("fixed+nullpad", new string[] { "0\00", "11", "22", "3 ", " 4", "55 5", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("fixed+spacepad", new string[] { "00", "11", "22", "3", " 4", "55 5", "66", "77", "", "AA", "ZZ", "!!" })]
        [InlineData("variable", new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("variable+spacepad", new string[] { "001", "1 1", "22", "33", "44", "55", "66", "77", "", "AA", "ZZ", "!!" })]
        [InlineData("variableUTF8", new string[] { "00", "111", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
        public void CanReadDataset_String(string name, string[] expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddString(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset($"/string/{name}");
                var actual = dataset.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadDataset_String_memory_selection()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddString(fileId, ContainerType.Dataset));

                var expected = new string?[] 
                { 
                    null, "001", null, "11", null, "22", null, "33", null, "44", null, "55", 
                    null, "66", null, "77", null, "  ", null, "AA", null, "ZZ", null, "!!"
                };

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset($"/string/variable");
                var memorySelection = new HyperslabSelection(start: 1, stride: 2, count: 12, block: 1);

                var actual = dataset.ReadString(
                    memorySelection: memorySelection, 
                    memoryDims: new ulong[] { 24 });

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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddBitField(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddOpaque(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddArray(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                    var elementType = expected.GetType().GetElementType()!;

                    var method = typeof(TestUtils).GetMethod(nameof(TestUtils.ReadAndCompare), BindingFlags.Public | BindingFlags.Static);
                    var generic = method!.MakeGenericMethod(elementType);
                    var result = (bool)generic.Invoke(null, new object[] { dataset, expected })!;

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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset_references = root.Group("reference").Dataset("region_reference");
                var references = dataset_references.Read<H5RegionReference>();

                var reference = references[0];
                root.Context.Reader.Seek((long)reference.CollectionAddress, SeekOrigin.Begin);

                // H5Rint.c (H5R__get_region)
                // TODO: use more structs?
                var globalHeapId = new GlobalHeapId(root.Context)
                {
                    CollectionAddress = reference.CollectionAddress,
                    ObjectIndex = reference.ObjectIndex
                };

                var globalHeapCollection = globalHeapId.Collection;
                var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex - 1];
                using var localReader = new H5StreamReader(new MemoryStream(globalHeapObject.ObjectData), leaveOpen: false);
                var address = root.Context.Superblock.ReadOffset(localReader);
                var selection = new DataspaceSelection(localReader);

                throw new NotImplementedException();
            });
        }

        [Fact]
        public void CanReadDataset_Shared_Message()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataWithSharedDataType(fileId, ContainerType.Dataset));
                var expected = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute_references = root.Group("shared_data_type").Dataset("shared_data_type");
                var actual = attribute_references.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

#if NET5_0_OR_GREATER
        [Fact]
        public void ThrowsForNestedNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct((long)fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset($"/struct/nullable");
                var exception = Assert.Throws<Exception>(() => dataset.ReadCompound<TestStructStringAndArrayL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }
#endif

        [Fact]
        public void CanReadDataset_External()
        {
            // INFO:
            // HDF lib says "external storage not supported with chunked layout". Same is true for compact layout.

            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddExternalDataset(fileId, "external_file"));
                var expected = TestData.MediumData.ToArray();

                for (int i = 33; i < 40; i++)
                {
                    expected[i] = 0;
                }

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("external");
                var dataset = parent.Dataset("external_file");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public async Task CanReadDataset_External_async()
        {
            // INFO:
            // HDF lib says "external storage not supported with chunked layout". Same is true for compact layout.

            await TestUtils.RunForAllVersionsAsync(async version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddExternalDataset(fileId, "external_file"));
                var expected = TestData.MediumData.ToArray();

                for (int i = 33; i < 40; i++)
                {
                    expected[i] = 0;
                }

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("external");
                var dataset = parent.Dataset("external_file");
                var actual = await dataset.ReadAsync<int>();

                // Assert
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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