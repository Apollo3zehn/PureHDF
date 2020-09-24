using HDF.PInvoke;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests
{
    public class HDF5Tests
    {
        private readonly ITestOutputHelper _logger;

        public HDF5Tests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Theory]
        [InlineData("/", true, false)]
        [InlineData("/simple", true, false)]
        [InlineData("/simple/sub?!", false, false)]
        [InlineData("/simple/sub?!", false, true)]
        public void CanCheckLinkExistsSimple(string path, bool expected, bool withEmptyFile)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId =>
                {
                    if (!withEmptyFile)
                        TestUtils.AddSimple(fileId);
                });

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var actual = root.LinkExists(path);

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Theory]
        [InlineData("/", true, false)]
        [InlineData("/mass_links", true, false)]
        [InlineData("/mass_links/mass_0000", true, false)]
        [InlineData("/mass_links/mass_0020", true, false)]
        [InlineData("/mass_links/mass_0102", true, false)]
        [InlineData("/mass_links/mass_0999", true, false)]
        [InlineData("/mass_links/mass_1000", false, false)]
        [InlineData("/mass_links/mass_1000", false, true)]
        public void CanCheckLinkExistsMass(string path, bool expected, bool withEmptyFile)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassLinks(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var actual = root.LinkExists(path);

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Theory]
        [InlineData("/", "/")]
        [InlineData("/simple", "simple")]
        [InlineData("/simple/sub", "sub")]
        public void CanOpenGroup(string path, string expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddSimple(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.Get<H5Group>(path);

                // Assert
                Assert.Equal(expected, group.Name);
            });
        }

        [Fact]
        public void CanEnumerateLinksMass()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassLinks(fileId));
                var expected = 1000;

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.Get<H5Group>("mass_links");

                // Assert
                var actual = group.Children.Count();
                Assert.Equal(expected, actual);
            });
        }

        [Theory]
        [InlineData("/D", "D")]
        [InlineData("/simple/D1", "D1")]
        [InlineData("/simple/sub/D1.1", "D1.1")]
        public void CanOpenDataset(string path, string expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddSimple(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.Get<H5Dataset>(path);

                // Assert
                Assert.Equal(expected, group.Name);
            });
        }

        public static IList<object[]> CanReadNumericalAttributeTestData => new List<object[]>
        {
            new object[] { "A1", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            new object[] { "A2", new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            new object[] { "A3", new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            new object[] { "A4", new ulong[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            new object[] { "A5", new sbyte[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
            new object[] { "A6", new short[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
            new object[] { "A7", new int[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
            new object[] { "A8", new long[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
            new object[] { "A9", new float[] { 0, 1, 2, 3, 4, 5, 6, (float)-7.99, 8, 9, 10, 11 } },
            new object[] {"A10", new double[] { 0, 1, 2, 3, 4, 5, 6, -7.99, 8, 9, 10, 11 } },
        };

        [Theory]
        [MemberData(nameof(HDF5Tests.CanReadNumericalAttributeTestData))]
        public void CanReadNumericalAttribute<T>(string name, T[] expected) where T : unmanaged
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.Get<H5Group>("typed").GetAttribute(name);
                var actual = attribute.Read<T>();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadNonNullableStructAttribute()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.Get<H5Group>("typed").GetAttribute("A14");
                var actual = attribute.Read<TestStructL1>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.NonNullableTestStructData));
            });
        }

        [Fact]
        public void CanReadNullableStructAttribute()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.Get<H5Group>("typed").GetAttribute("A15");
                var actual = attribute.ReadCompound<TestStructString>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.StringTestStructData));
            });
        }

        [Fact]
        public void CanReadTinyAttribute()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTinyAttribute(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("tiny");
                var attribute = parent.Attributes.First();
                var actual = attribute.Read<byte>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.TinyData));
            });
        }

        [Fact]
        public void CanReadHugeAttribute()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddHugeAttribute(fileId, version));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("large");
                var attribute = parent.Attributes.First();
                var actual = attribute.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.HugeData[0..actual.Length]));
            });
        }

        [Theory]
        [InlineData("mass_0000", true)]
        [InlineData("mass_0020", true)]
        [InlineData("mass_0102", true)]
        [InlineData("mass_0999", true)]
        [InlineData("mass_1000", false)]
        public void CanCheckAttributeExistsMass(string attributeName, bool expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("mass_attributes");
                var actual = parent.AttributeExists(attributeName);

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Fact]
        public void CanCheckAttributeExistsUTF8()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassAttributes(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("mass_attributes");
            var actual = parent.AttributeExists("字形碼 / 字形码, Zìxíngmǎ");

            // Assert
            Assert.True(actual);
        }


        [Fact]
        public void CanReadMassAmountOfAttributes()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassAttributes(fileId));
                var expectedCount = 1000;

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("mass_attributes");
                var attributes = parent.Attributes.ToList();

                foreach (var attribute in attributes)
                {
                    var actual = attribute.ReadCompound<TestStructL1>();

                    // Assert
                    Assert.True(actual.SequenceEqual(TestUtils.NonNullableTestStructData));
                }

                Assert.Equal(expectedCount, attributes.Count);
            });
        }

        [Fact]
        public void ThrowsForNestedNullableStructAttribute()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.Get<H5Group>("typed").GetAttribute("A15");
                var exception = Assert.Throws<Exception>(() => attribute.ReadCompound<TestStructStringL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }

        // Fixed-length string attribute (UTF8) is not supported because 
        // it is incompatible with variable byte length per character.
        [Theory]
        [InlineData("A11", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("A12", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("A13", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
        public void CanReadStringAttribute(string name, string[] expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.Get<H5Group>("typed").GetAttribute(name);
                var actual = attribute.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanFollowLinks()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddLinks(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);

                var dataset_hard_1 = root.Get<H5Dataset>("links/hard_link_1/dataset");
                var dataset_hard_2 = root.Get<H5Dataset>("links/hard_link_2/dataset");
                var dataset_soft_2 = root.Get<H5Dataset>("links/soft_link_2/dataset");
                var dataset_direct = root.Get<H5Dataset>("links/dataset");
            });
        }

        [Fact]
        public void CanOpenLink()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddLinks(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);

                var group = root.GetSymbolicLink("links/soft_link_2");
                var dataset = root.GetSymbolicLink("links/dataset");
            });
        }

        [Fact]
        public void CanReadWrappedFiles()
        {
            // Arrange
            var filePath = "testfiles/secret.mat";

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var children = root.Children.ToList();
        }

        [Theory]
        [InlineData("Deadbeef", 0x5c16ad42)]
        [InlineData("f", 0xb3e7e36f)]
        [InlineData("字形碼 / 字形码, Zìxíngmǎ", 0xfd18335c)]
        public void CanCalculateHash(string key, uint expected)
        {
            // Arrange

            // Act
            var actual = H5Checksum.JenkinsLookup3(key);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanReadCompactDataset()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddCompactDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("compact");
                var dataset = parent.Get<H5Dataset>("compact");
                var actual = dataset.Read<byte>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.TinyData));
            });
        }


        [Fact]
        public void CanReadCompactDatasetTestFile()
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
                var dataset = parent.Get<H5Dataset>("DS1");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(expected.Cast<int>()));
            });
        }

        [Fact]
        public void CanReadContiguousDataset()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddContiguousDataset(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("contiguous");
                var dataset = parent.Get<H5Dataset>("contiguous");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.HugeData));
            });
        }

        [Fact]
        public void CanReadChunkedDataset()
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
                    var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset(fileId, withShuffle));

                    // Act
                    using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                    var parent = root.Get<H5Group>("chunked");
                    var dataset = parent.Get<H5Dataset>("chunked");
                    var actual = dataset.Read<int>();

                    // Assert
                    Assert.True(actual.SequenceEqual(TestUtils.MediumData));
                }
            });
        }

        [Fact]
        public void CanReadChunkedDatasetSingleChunk()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Single_Chunk(fileId, withShuffle));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("chunked");
                var dataset = parent.Get<H5Dataset>("chunked_single_chunk");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.MediumData));
            }
        }

        [Fact]
        public void CanReadChunkedDatasetImplicit()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Implicit(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("chunked");
            var dataset = parent.Get<H5Dataset>("chunked_implicit");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestUtils.MediumData));
        }

        [Fact]
        public void CanReadChunkedDatasetFixedArray()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Fixed_Array(fileId, withShuffle));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("chunked");
                var dataset = parent.Get<H5Dataset>("chunked_fixed_array");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.MediumData));
            }
        }

        [Fact]
        public void CanReadChunkedDatasetFixedArrayPaged()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Fixed_Array_Paged(fileId, withShuffle));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("chunked");
                var dataset = parent.Get<H5Dataset>("chunked_fixed_array_paged");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.MediumData));
            }
        }

        [Fact]
        public void CanReadChunkedDatasetExtensibleArrayElements()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Extensible_Array_Elements(fileId, withShuffle));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("chunked");
                var dataset = parent.Get<H5Dataset>("chunked_extensible_array_elements");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.MediumData));
            }
        }

        [Fact]
        public void CanReadChunkedDatasetExtensibleArrayDataBlocks()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Extensible_Array_Data_Blocks(fileId, withShuffle));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("chunked");
                var dataset = parent.Get<H5Dataset>("chunked_extensible_array_data_blocks");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.MediumData));
            }
        }

        [Fact]
        public void CanReadChunkedDatasetExtensibleArraySecondaryBlocks()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_Extensible_Array_Secondary_Blocks(fileId, withShuffle));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("chunked");
                var dataset = parent.Get<H5Dataset>("chunked_extensible_array_secondary_blocks");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.MediumData));
            }
        }

        [Fact]
        public void CanReadChunkedDatasetBTree2()
        {
            foreach (var withShuffle in new bool[] { false, true })
            {
                // Arrange
                var version = H5F.libver_t.LATEST;
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.Get<H5Group>("chunked");
                var dataset = parent.Get<H5Dataset>("chunked_btree2");
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestUtils.MediumData));
            }
        }

        [Theory]
        [InlineData("blosclz", true)]
        [InlineData("lz4", true)]
        [InlineData("lz4hc", true)]
        [InlineData("snappy", false)]
        [InlineData("zlib", true)]
        [InlineData("zstd", true)]
        [InlineData("blosclz_bit", true)]
        public void CanDefilterBlosc2(string datasetName, bool shouldSuccess)
        {
            // import h5py
            // import hdf5plugin

            // def blosc_opts(complevel=9, complib='blosc:lz4', shuffle=True):
            //     shuffle = 2 if shuffle == 'bit' else 1 if shuffle else 0
            //     compressors = ['blosclz', 'lz4', 'lz4hc', 'snappy', 'zlib', 'zstd']
            //     complib = ['blosc:' + c for c in compressors].index(complib)
            //     args = {
            //         'compression': 32001,
            //         'compression_opts': (0, 0, 0, 0, complevel, shuffle, complib)
            //     }
            //     if shuffle:
            //         args['shuffle'] = False
            //     return args

            // with h5py.File('blosc.h5', 'w') as f:
            //     f.create_dataset('blosclz', data=list(range(0, 1000)), **blosc_opts(9, 'blosc:blosclz', True))
            //     f.create_dataset('lz4', data=list(range(0, 1000)), **blosc_opts(9, 'blosc:lz4', True))
            //     f.create_dataset('lz4hc', data=list(range(0, 1000)), **blosc_opts(9, 'blosc:lz4hc', True))
            //     f.create_dataset('snappy', data=list(range(0, 1000)), **blosc_opts(9, 'blosc:snappy', True))
            //     f.create_dataset('zlib', data=list(range(0, 1000)), **blosc_opts(9, 'blosc:zlib', True))
            //     f.create_dataset('zstd', data=list(range(0, 1000)), **blosc_opts(9, 'blosc:zstd', True))
            //     f.create_dataset('blosclz_bit', data=list(range(0, 1000)), **blosc_opts(9, 'blosc:blosclz', 'bit'))

            // Arrange
            var filePath = "./testfiles/blosc.h5";
            var expected = Enumerable.Range(0, 1000).ToArray();

            H5Filter.Register(identifier: (FilterIdentifier)32001, name: "blosc2", filterFunc: BloscHelper.FilterFunc);

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dataset = root.Get<H5Dataset>(datasetName);

            if (shouldSuccess)
            {
                var actual = dataset.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            }
            else
            {
                var exception = Assert.Throws<Exception>(() => dataset.Read<int>());

                // Assert
                Assert.Contains("snappy", exception.InnerException.Message);
            }
        }

        [Fact]
        public void CanDefilterFletcher()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddFilteredDataset_Fletcher(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("filtered");
            var dataset = parent.Get<H5Dataset>("fletcher");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestUtils.MediumData));
        }

        [Fact]
        public void CanDefilterZLib()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddFilteredDataset_ZLib(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("filtered");
            var dataset = parent.Get<H5Dataset>("deflate");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestUtils.MediumData));
        }

        [Fact]
        public void CanDefilterMultiple()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddFilteredDataset_Multi(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("filtered");
            var dataset = parent.Get<H5Dataset>("multi");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestUtils.MediumData));
        }

        [Fact]
        public void CanConvertDataset2D()
        {
            // Arrange
            var expected = new int[4, 7];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    expected[i, j] = i * j - j;
                }
            }

            // Act
            var actual = expected
                .Cast<int>()
                .ToArray()
                .ToArray2D(new long[] { 4, -1 });

            // Assert
            Assert.Equal(expected.Rank, actual.Rank);

            for (int i = 0; i < expected.Rank; i++)
            {
                Assert.Equal(expected.GetLength(i), actual.GetLength(i));
            }
            Assert.True(actual.Cast<int>().SequenceEqual(expected.Cast<int>()));
        }

        [Fact]
        public void CanConvertDataset3D()
        {
            // Arrange
            var expected = new int[4, 7, 2];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        expected[i, j, k] = i * j - j + k;
                    }
                }
            }

            // Act
            var actual = expected
                .Cast<int>()
                .ToArray()
                .ToArray3D(new long[] { -1, 7, 2 });

            // Assert
            Assert.Equal(expected.Rank, actual.Rank);

            for (int i = 0; i < expected.Rank; i++)
            {
                Assert.Equal(expected.GetLength(i), actual.GetLength(i));
            }

            Assert.True(actual.Cast<int>().SequenceEqual(expected.Cast<int>()));
        }

#warning 16 byte and arbitrary number of bytes tests missing
        [Theory]
        [InlineData((byte)1, 1001)]
        [InlineData((short)2, 732)]
        [InlineData((short)2, 733)]
        [InlineData((int)4, 732)]
        [InlineData((int)4, 733)]
        [InlineData((int)4, 734)]
        [InlineData((int)4, 735)]
        [InlineData((long)8, 432)]
        [InlineData((long)8, 433)]
        [InlineData((long)8, 434)]
        [InlineData((long)8, 435)]
        [InlineData((long)8, 436)]
        [InlineData((long)8, 437)]
        [InlineData((long)8, 438)]
        [InlineData((long)8, 439)]
        public void CanUnshuffleDataGeneric<T>(T dummy, int length) where T : unmanaged
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("filtered");
            var dataset = parent.Get<H5Dataset>($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(skipShuffle: true);

            // Act
            var actual = new byte[actual_shuffled.Length];
            ShuffleGeneric.Unshuffle(bytesOfType, actual_shuffled, actual);

            // Assert
            Assert.True(actual.AsSpan().SequenceEqual(expected));
        }

        [Theory]
        [InlineData((byte)1, 1001)]
        [InlineData((short)2, 732)]
        [InlineData((short)2, 733)]
        [InlineData((int)4, 732)]
        [InlineData((int)4, 733)]
        [InlineData((int)4, 734)]
        [InlineData((int)4, 735)]
        [InlineData((long)8, 432)]
        [InlineData((long)8, 433)]
        [InlineData((long)8, 434)]
        [InlineData((long)8, 435)]
        [InlineData((long)8, 436)]
        [InlineData((long)8, 437)]
        [InlineData((long)8, 438)]
        [InlineData((long)8, 439)]
        public void CanUnshuffleDataAvx2<T>(T dummy, int length) where T : unmanaged
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: false);
            var parent = root.Get<H5Group>("filtered");
            var dataset = parent.Get<H5Dataset>($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(skipShuffle: true);

            // Act
            var actual = new byte[actual_shuffled.Length];
            ShuffleAvx2.Unshuffle(bytesOfType, actual_shuffled, actual);

            // Assert
            Assert.True(actual.AsSpan().SequenceEqual(expected));
        }

        [Theory]
        [InlineData((byte)1, 1001)]
        [InlineData((short)2, 732)]
        [InlineData((short)2, 733)]
        [InlineData((int)4, 732)]
        [InlineData((int)4, 733)]
        [InlineData((int)4, 734)]
        [InlineData((int)4, 735)]
        [InlineData((long)8, 432)]
        [InlineData((long)8, 433)]
        [InlineData((long)8, 434)]
        [InlineData((long)8, 435)]
        [InlineData((long)8, 436)]
        [InlineData((long)8, 437)]
        [InlineData((long)8, 438)]
        [InlineData((long)8, 439)]
        public void CanUnshuffleDataSse2<T>(T dummy, int length) where T : unmanaged
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("filtered");
            var dataset = parent.Get<H5Dataset>($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(skipShuffle: true);

            // Act
            var actual = new byte[actual_shuffled.Length];
            ShuffleAvx2.Unshuffle(bytesOfType, actual_shuffled, actual);

            // Assert
            Assert.True(actual.AsSpan().SequenceEqual(expected));
        }

        [Fact]
        public void ShufflePerformanceTest()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var length = 10_000_000;
            var bytesOfType = Unsafe.SizeOf<int>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Get<H5Group>("shuffle");
            var dataset = parent.Get<H5Dataset>($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(skipShuffle: true);

            // Act

            /* generic */
            var sw = Stopwatch.StartNew();
            var actual = new byte[actual_shuffled.Length];
            ShuffleGeneric.Unshuffle(bytesOfType, actual_shuffled, actual);
            var generic = sw.Elapsed.TotalMilliseconds;
            _logger.WriteLine($"Generic: {generic:F1} ms");

            /* SSE2 */
            if (Sse2.IsSupported)
            {
                sw = Stopwatch.StartNew();
                actual = new byte[actual_shuffled.Length];
                ShuffleAvx2.Unshuffle(bytesOfType, actual_shuffled, actual);
                var sse2 = sw.Elapsed.TotalMilliseconds;
                _logger.WriteLine($"Generic: {sse2:F1} ms");
            }

            /* AVX2 */
            if (Avx2.IsSupported)
            {
                sw = Stopwatch.StartNew();
                actual = new byte[actual_shuffled.Length];
                ShuffleAvx2.Unshuffle(bytesOfType, actual_shuffled, actual);
                var avx2 = sw.Elapsed.TotalMilliseconds;
                _logger.WriteLine($"Generic: {avx2:F1} ms");
            }
        }
    }
}