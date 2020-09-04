using HDF.PInvoke;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HDF5.NET.Tests
{
    public class HDF5Tests
    {
        [Fact]
        public unsafe void Dummy()
        {
            // Arrange
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug();
            });

            var logger = loggerFactory.CreateLogger("");

            var filePath = Path.GetTempFileName();
            long res;

            var fileId = H5F.create(filePath, H5F.ACC_TRUNC);
            var groupId1 = H5G.create(fileId, "G1");
            var groupId2 = H5G.create(fileId, "G2");
            var groupId3 = H5G.create(groupId1, "G1.1");
            var dataspaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId = H5D.create(fileId, "D1", H5T.NATIVE_INT8, dataspaceId);

            var attributeDataspaceId1 = H5S.create_simple(1, new ulong[] { 2 }, new ulong[] { 2 });
            var attributeId1 = H5A.create(fileId, "A0", H5T.NATIVE_DOUBLE, attributeDataspaceId1);
            var attributeData1 = new double[] { 0.1, 0.2 };

            fixed (double* ptr = attributeData1)
            {
                res = H5A.write(attributeId1, H5T.NATIVE_DOUBLE, new IntPtr((void*)ptr));
            }

            var attributeDataspaceId2 = H5S.create_simple(1, new ulong[] { 2 }, new ulong[] { 4 });
            var attributeId2 = H5A.create(groupId3, "A1.1.1", H5T.NATIVE_DOUBLE, attributeDataspaceId1);
            var attributeData2 = new double[] { 2e-31, 99.98e+2 };

            fixed (double* ptr = attributeData2)
            {
                res = H5A.write(attributeId2, H5T.NATIVE_DOUBLE, new IntPtr((void*)ptr));
            }

            var attributeDataspaceId3 = H5S.create_simple(1, new ulong[] { 2 }, new ulong[] { 3 });
            var attributeId3 = H5A.create(groupId3, "A1.1.2", H5T.NATIVE_INT64, attributeDataspaceId1);
            var attributeData3 = new long[] { 0, 65561556, 2 };

            fixed (long* ptr = attributeData3)
            {
                res = H5A.write(attributeId3, H5T.NATIVE_INT64, new IntPtr((void*)ptr));
            }

            res = H5A.close(attributeId3);
            res = H5D.close(attributeDataspaceId3);

            res = H5A.close(attributeId2);
            res = H5D.close(attributeDataspaceId2);

            res = H5A.close(attributeId1);
            res = H5D.close(attributeDataspaceId1);

            res = H5D.close(datasetId);
            res = H5S.close(dataspaceId);

            res = H5G.close(groupId3);
            res = H5G.close(groupId2);
            res = H5G.close(groupId1);

            res = H5F.close(fileId);

            // Act
            using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            h5file.Superblock.Print(logger);

            var children = h5file.Root.Children;

            //h5file.OpenGroup("/Test");

            //// Assert
            //Assert.Throws<FormatException>(action);
        }

        [Theory]
        [InlineData("/", true)]
        [InlineData("/simple", true)]
        [InlineData("/simple/sub?!", false)]
        public void CanCheckExists(string path, bool expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

                var actual = root.LinkExists(path);

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Theory]
        [InlineData("/", "/")]
        [InlineData("/simple", "simple")]
        [InlineData("/simple/sub", "G1.1")]
        public void CanOpenGroup(string path, string expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

                var group = root.Get<H5Group>(path);

                // Assert
                Assert.Equal(expected, group.Name);
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
                var filePath = TestUtils.PrepareTestFile(version);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

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
                var filePath = TestUtils.PrepareTestFile(version, withTypedAttributes: true);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

                var attribute = root.Get<H5Group>("/typed").Attributes.First(attribute => attribute.Name == name);
                var actual = attribute.Read<T>().ToArray();

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
                var filePath = TestUtils.PrepareTestFile(version, withTypedAttributes: true);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

                var attribute = root.Get<H5Group>("/typed").Attributes.First(attribute => attribute.Name == "A14");
                var actual = attribute.Read<TestStructL1>().ToArray();

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
                var filePath = TestUtils.PrepareTestFile(version, withTypedAttributes: true);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

                var attribute = root.Get<H5Group>("/typed").Attributes.First(attribute => attribute.Name == "A15");
                var actual = attribute.ReadCompound<TestStructString>().ToArray();

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
                var filePath = TestUtils.PrepareTestFile(version, withTinyAttribute: true);

                // Act
                using (var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var parent = h5file.Root.Get<H5Group>("/tiny");
                    var attribute = parent.Attributes.First();
                    var actual = attribute.Read<byte>().ToArray();

                    // Assert
                    Assert.True(actual.SequenceEqual(TestUtils.TinyData));
                }

                File.Delete(filePath);
            });
        }

        [Fact]
        public void CanReadHugeAttribute()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, withHugeAttribute: true);

                // Act
                using (var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var parent = h5file.Root.Get<H5Group>("/large");
                    var attribute = parent.Attributes.First();
                    var actual = attribute.Read<int>().ToArray();

                    // Assert
                    Assert.True(actual.SequenceEqual(TestUtils.HugeData[0..actual.Length]));
                }

                File.Delete(filePath);
            });
        }

        [Fact]
        public void CanReadMassAmountOfAttributes()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, withMassAttributes: true);
                var expectedCount = 1000;

                // Act
                using (var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var parent = h5file.Root.Get<H5Group>("/mass");
                    var attributes = parent.Attributes.ToList();

                    foreach (var attribute in attributes)
                    {
                        var actual = attribute.ReadCompound<TestStructL1>().ToArray();

                        // Assert
                        Assert.True(actual.SequenceEqual(TestUtils.NonNullableTestStructData));
                    }

                    Assert.Equal(expectedCount, attributes.Count);
                }

                File.Delete(filePath);
            });
        }

        [Fact]
        public void ThrowsForNestedNullableStructAttribute()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, withTypedAttributes: true);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

                var attribute = root.Get<H5Group>("/typed").Attributes.First(attribute => attribute.Name == "A15");
                var exception = Assert.Throws<Exception>(() => attribute.ReadCompound<TestStructStringL1>().ToArray());

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
                var filePath = TestUtils.PrepareTestFile(version, withTypedAttributes: true);

                // Act
                using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var root = h5file.Root;

                var attribute = root.Get<H5Group>("/typed").Attributes.First(attribute => attribute.Name == name);
                var actual = attribute.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }
    }
}