using System.Runtime.InteropServices;
using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public partial class DatasetTests
    {
        [Fact]
        public void CanReadDataset_Virtual_sync()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId 
                => TestUtils.AddVirtualDataset(fileId, "virtual"));

            var expected = new int[] { 2, 3, 17, 8, 21, 25, -1, -1 };

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var selection = new HyperslabSelection(start: 3, stride: 4, count: 4, block: 2);
            var actual = dataset.Read<int>(selection);

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

#if NET6_0_OR_GREATER
        [Fact]
        public async Task CanReadDataset_Virtual_async()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId 
                => TestUtils.AddVirtualDataset(fileId, "virtual"));

            var expected = new int[] { 2, 3, 17, 8, 21, 25, -1, -1 };

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var selection = new HyperslabSelection(start: 3, stride: 4, count: 4, block: 2);
            var actual = await dataset.ReadAsync<int>(selection);

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }
#endif

        [Fact]
        public void CanReadDataset_Virtual_source_point()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.AddVirtualDataset_source_point(fileId, "virtual"));

            /* fix https://github.com/HDFGroup/hdf5/blob/hdf5_1_10/src/H5Dvirtual.c#L175-L177 */
            {
                using var writer = new BinaryWriter(File.OpenWrite(filePath));
                writer.Seek(0x83C, SeekOrigin.Begin);

                // type (point selection with special handling)
                writer.Write(uint.MaxValue);

                // jump position (special handling)
                writer.Write((uint)0xC4);

                // version
                writer.Write((uint)2);

                // encode size
                writer.Write((byte)2);

                // rank
                writer.Write((uint)2);

                // num points
                writer.Write((ushort)10);

                var points = new ushort[] {
                    01, 03,
                    02, 04,
                    03, 05,
                    11, 09,
                    09, 04,
                    08, 00,
                    05, 04,
                    05, 03,
                    05, 09,
                    09, 01
                };

                writer.Write(MemoryMarshal.AsBytes(points.AsSpan()));
            }
            /* end fix */

            var expected = new int[] 
            { 
                13, 24, 35, 119, 94, 80, 54, 53, 59, 91
            };

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanReadDataset_Virtual_source_irregular()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId 
                => TestUtils.AddVirtualDataset_source_irregular(fileId, "virtual"));

            var expected = new int[] 
            { 
                13, 14, 15,
                23, 24, 25,
                33, 34, 35,

                53, 54, 55,
                61, 62, 63, 64, 65, 66, 67,
                71, 72, 73, 74, 75, 76, 77,
                81, 82, 83, 85, 86, 87,
                
                93, 94, 95,
                103, 104, 105,
                113, 114, 115,
            };

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanReadDataset_Virtual_source_regular()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId 
                => TestUtils.AddVirtualDataset_source_regular(fileId, "virtual"));

            var expected = new int[90];
            var index = 0;

            for (int row = 1; row < 40; row++)
            {
                if (row % 4 == 0)
                    continue;

                for (int column = 3; column < 6; column++)
                {
                    expected[index] = row * 10 + column;
                    index++;
                }
            }

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanReadDataset_Virtual_virtual_point()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.AddVirtualDataset_virtual_point(fileId, "virtual"));

            /* fix https://github.com/HDFGroup/hdf5/blob/hdf5_1_10/src/H5Dvirtual.c#L175-L177 */
            {
                using var writer = new BinaryWriter(File.OpenWrite(filePath));
                writer.Seek(0x84C, SeekOrigin.Begin);

                // type (point selection with special handling)
                writer.Write(uint.MaxValue);

                // jump position (special handling)
                writer.Write((uint)0xD4);

                // version
                writer.Write((uint)2);

                // encode size
                writer.Write((byte)2);

                // rank
                writer.Write((uint)2);

                // num points
                writer.Write((ushort)10);

                var points = new ushort[] {
                    01, 03,
                    02, 04,
                    03, 05,
                    11, 09,
                    09, 04,
                    08, 00,
                    05, 04,
                    05, 03,
                    05, 09,
                    09, 01
                };

                writer.Write(MemoryMarshal.AsBytes(points.AsSpan()));
            }
            /* end fix */

            var expected = new int[130];

            var hasValue = new int[] 
            { 
                13, 24, 35, 119, 94, 80, 54, 53, 59, 91
            };

            for (int i = 0; i < hasValue.Length; i++)
            {
                expected[hasValue[i]] = i;
            }

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanReadDataset_Virtual_virtual_irregular()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.AddVirtualDataset_virtual_irregular(fileId, "virtual"));

            var expected = new int[130];

            var hasValue = new int[] 
            { 
                13, 14, 15,
                23, 24, 25,
                33, 34, 35,

                53, 54, 55,
                61, 62, 63, 64, 65, 66, 67,
                71, 72, 73, 74, 75, 76, 77,
                81, 82, 83, 85, 86, 87,
                
                93, 94, 95,
                103, 104, 105,
                113, 114, 115,
            };

            for (int i = 0; i < hasValue.Length; i++)
            {
                expected[hasValue[i]] = i;
            }

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanReadDataset_Virtual_virtual_regular()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.AddVirtualDataset_virtual_regular(fileId, "virtual"));

            var expected = new int[400];
            var index = 0;

            for (int row = 1; row < 40; row++)
            {
                if (row % 4 == 0)
                    continue;

                for (int column = 3; column < 6; column++)
                {
                    expected[row * 10 + column] = index;
                    index++;
                }
            }

            // Act
            using var root = H5NativeFile.OpenRead(filePath, deleteOnClose: false);
            var dataset = root.Dataset("vds");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }
    }
}