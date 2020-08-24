using HDF.PInvoke;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        [InlineData("/G1", true)]
        [InlineData("/G1/G?!", false)]
        public void CanCheckExists(string path, bool expected)
        {
            // Arrange
            var filePath = this.PrepareTestFile();

            // Act
            using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var root = h5file.Root;

            var actual = root.LinkExists(path);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("/", "/")]
        [InlineData("/G1", "G1")]
        [InlineData("/G1/G1.1", "G1.1")]
        public void CanOpenGroup(string path, string expected)
        {
            // Arrange
            var filePath = this.PrepareTestFile();

            // Act
            using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var root = h5file.Root;

            var group = root.GetDescendant<H5Group>(path);

            // Assert
            Assert.Equal(expected, group.Name);
        }

        [Theory]
        [InlineData("/D", "D")]
        [InlineData("/G1/D1", "D1")]
        [InlineData("/G1/G1.1/D1.1", "D1.1")]
        public void CanOpenDataset(string path, string expected)
        {
            // Arrange
            var filePath = this.PrepareTestFile();

            // Act
            using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var root = h5file.Root;

            var group = root.GetDescendant<H5Dataset>(path);

            // Assert
            Assert.Equal(expected, group.Name);
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
            // Arrange
            var filePath = this.PrepareTestFile(withAttributes: true);

            // Act
            using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var root = h5file.Root;

            var attribute = root.Attributes.First(attribute => attribute.Name == name);
            var actual = attribute.Read<T>().ToArray();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        // Fixed-length string attribute (UTF8) is not supported because 
        // it is incompatible with variable byte length per character.
        [Theory]
        [InlineData("A11", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("A12", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("A13", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
        public void CanReadStringAttribute(string name, string[] expected)
        {
            // Arrange
            var filePath = this.PrepareTestFile(withAttributes: true);

            // Act
            using var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var root = h5file.Root;

            var attribute = root.Attributes.First(attribute => attribute.Name == name);
            var actual = attribute.ReadAsStringArray();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        public unsafe string PrepareTestFile(bool withAttributes = false)
        {
            var filePath = Path.GetTempFileName();
            long res;

            // file
            var fileId = H5F.create(filePath, H5F.ACC_TRUNC);

            // groups
            var groupId1 = H5G.create(fileId, "G1");
            var groupId1_1 = H5G.create(groupId1, "G1.1");

            // datasets
            var dataspaceId1 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId1 = H5D.create(fileId, "D", H5T.NATIVE_INT8, dataspaceId1);

            var dataspaceId2 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId2 = H5D.create(groupId1, "D1", H5T.NATIVE_INT8, dataspaceId1);

            var dataspaceId3 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId3 = H5D.create(groupId1_1, "D1.1", H5T.NATIVE_INT8, dataspaceId1);

            res = H5D.close(datasetId1);
            res = H5S.close(dataspaceId1);

            res = H5D.close(datasetId2);
            res = H5S.close(dataspaceId2);

            res = H5D.close(datasetId3);
            res = H5S.close(dataspaceId3);

            if (withAttributes)
            {
                var attributeSpaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 3, 3, 4 });

                // numeric attributes
                var attributeId1 = H5A.create(fileId, "A1", H5T.NATIVE_UINT8, attributeSpaceId);
                var attributeData1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};

                fixed (void* ptr = attributeData1)
                {
                    res = H5A.write(attributeId1, H5T.NATIVE_UINT8, new IntPtr(ptr));
                }

                H5A.close(attributeId1);

                var attributeData2 = new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};
                var attributeId2 = H5A.create(fileId, "A2", H5T.NATIVE_UINT16, attributeSpaceId);

                fixed (void* ptr = attributeData2)
                {
                    res = H5A.write(attributeId2, H5T.NATIVE_UINT16, new IntPtr(ptr));
                }

                H5A.close(attributeId2);

                var attributeId3 = H5A.create(fileId, "A3", H5T.NATIVE_UINT32, attributeSpaceId);
                var attributeData3 = new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};

                fixed (void* ptr = attributeData3)
                {
                    res = H5A.write(attributeId3, H5T.NATIVE_UINT32, new IntPtr(ptr));
                }

                H5A.close(attributeId3);

                var attributeId4 = H5A.create(fileId, "A4", H5T.NATIVE_UINT64, attributeSpaceId);
                var attributeData4 = new ulong[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};

                fixed (void* ptr = attributeData4)
                {
                    res = H5A.write(attributeId4, H5T.NATIVE_UINT64, new IntPtr(ptr));
                }

                H5A.close(attributeId4);

                var attributeId5 = H5A.create(fileId, "A5", H5T.NATIVE_INT8, attributeSpaceId);
                var attributeData5 = new sbyte[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11};

                fixed (void* ptr = attributeData5)
                {
                    res = H5A.write(attributeId5, H5T.NATIVE_INT8, new IntPtr(ptr));
                }

                H5A.close(attributeId5);

                var attributeId6 = H5A.create(fileId, "A6", H5T.NATIVE_INT16, attributeSpaceId);
                var attributeData6 = new short[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11};

                fixed (void* ptr = attributeData6)
                {
                    res = H5A.write(attributeId6, H5T.NATIVE_INT16, new IntPtr(ptr));
                }

                H5A.close(attributeId6);

                var attributeId7 = H5A.create(fileId, "A7", H5T.NATIVE_INT32, attributeSpaceId);
                var attributeData7 = new int[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11};

                fixed (void* ptr = attributeData7)
                {
                    res = H5A.write(attributeId7, H5T.NATIVE_INT32, new IntPtr(ptr));
                }

                H5A.close(attributeId7);

                var attributeId8 = H5A.create(fileId, "A8", H5T.NATIVE_INT64, attributeSpaceId);
                var attributeData8 = new long[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11};

                fixed (void* ptr = attributeData8)
                {
                    res = H5A.write(attributeId8, H5T.NATIVE_INT64, new IntPtr(ptr));
                }

                H5A.close(attributeId8);

                var attributeId9 = H5A.create(fileId, "A9", H5T.NATIVE_FLOAT, attributeSpaceId);
                var attributeData9 = new float[] { 0, 1, 2, 3, 4, 5, 6, (float)-7.99, 8, 9, 10, 11};

                fixed (void* ptr = attributeData9)
                {
                    res = H5A.write(attributeId9, H5T.NATIVE_FLOAT, new IntPtr(ptr));
                }

                H5A.close(attributeId9);

                var attributeId10 = H5A.create(fileId, "A10", H5T.NATIVE_DOUBLE, attributeSpaceId);
                var attributeData10 = new double[] { 0, 1, 2, 3, 4, 5, 6, -7.99, 8, 9, 10, 11};

                fixed (void* ptr = attributeData10)
                {
                    res = H5A.write(attributeId10, H5T.NATIVE_DOUBLE, new IntPtr(ptr));
                }

                H5A.close(attributeId10);

                // fixed length string attribute (ASCII)
                var attributeTypeId11 = H5T.copy(H5T.C_S1);
                res = H5T.set_size(attributeTypeId11, new IntPtr(2));
                res = H5T.set_cset(attributeTypeId11, H5T.cset_t.ASCII);

                var attributeId11 = H5A.create(fileId, "A11", attributeTypeId11, attributeSpaceId);
                var attributeData11 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
                var attributeData11Char = attributeData11
                    .SelectMany(value => Encoding.ASCII.GetBytes(value))
                    .ToArray();

                fixed (void* ptr = attributeData11Char)
                {
                    res = H5A.write(attributeId11, attributeTypeId11, new IntPtr(ptr));
                }

                H5T.close(attributeTypeId11);
                H5A.close(attributeId11);

                // variable length string attribute (ASCII)
                var attributeTypeId12 = H5T.copy(H5T.C_S1);
                res = H5T.set_size(attributeTypeId12, H5T.VARIABLE);
                res = H5T.set_cset(attributeTypeId12, H5T.cset_t.ASCII);

                var attributeId12 = H5A.create(fileId, "A12", attributeTypeId12, attributeSpaceId);
                var attributeData12 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
                var attributeData12IntPtr = attributeData12.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

                fixed (void* ptr = attributeData12IntPtr)
                {
                    res = H5A.write(attributeId12, attributeTypeId12, new IntPtr(ptr));
                }

                foreach (var ptr in attributeData12IntPtr)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }             

                H5T.close(attributeTypeId12);
                H5A.close(attributeId12);

                // variable length string attribute (UTF8)
                var attributeTypeId13 = H5T.copy(H5T.C_S1);
                res = H5T.set_size(attributeTypeId13, H5T.VARIABLE);
                res = H5T.set_cset(attributeTypeId13, H5T.cset_t.UTF8);

                var attributeId13 = H5A.create(fileId, "A13", attributeTypeId13, attributeSpaceId);
                var attributeData13 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" };
                var attributeData13IntPtr = attributeData13.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

                fixed (void* ptr = attributeData13IntPtr)
                {
                    res = H5A.write(attributeId13, attributeTypeId13, new IntPtr(ptr));
                }

                foreach (var ptr in attributeData13IntPtr)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }

                H5T.close(attributeTypeId13);
                H5A.close(attributeId13);

                //
                H5S.close(attributeSpaceId);
            }

            res = H5G.close(groupId1_1);
            res = H5G.close(groupId1);

            res = H5F.close(fileId);

            return filePath;
        }
    }
}