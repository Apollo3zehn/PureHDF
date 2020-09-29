using HDF.PInvoke;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests.Reading
{
    public class FilterTests
    {
        private readonly ITestOutputHelper _logger;

        public FilterTests(ITestOutputHelper logger)
        {
            _logger = logger;
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
            var dataset = root.Dataset(datasetName);

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
            var parent = root.Group("filtered");
            var dataset = parent.Dataset("fletcher");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestData.MediumData));
        }

        [Fact]
        public void CanDefilterZLib()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddFilteredDataset_ZLib(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset("deflate");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestData.MediumData));
        }

        [Fact]
        public void CanDefilterMultiple()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddFilteredDataset_Multi(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset("multi");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestData.MediumData));
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
        public void CanUnshuffleGeneric<T>(T dummy, int length) where T : struct
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
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
        public void CanUnshuffleAvx2<T>(T dummy, int length) where T : struct
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: false);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
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
        public void CanUnshuffleSse2<T>(T dummy, int length) where T : struct
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
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
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
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
                _logger.WriteLine($"SSE2: {sse2:F1} ms");
            }

            /* AVX2 */
            if (Avx2.IsSupported)
            {
                sw = Stopwatch.StartNew();
                actual = new byte[actual_shuffled.Length];
                ShuffleAvx2.Unshuffle(bytesOfType, actual_shuffled, actual);
                var avx2 = sw.Elapsed.TotalMilliseconds;
                _logger.WriteLine($"AVX2: {avx2:F1} ms");
            }
        }


        [Theory]
        [InlineData(1, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x9 })]
        [InlineData(2, new byte[] { 0x01, 0x00, 0x03, 0x02, 0x05, 0x04, 0x07, 0x06, 0x09, 0x8 })]
        [InlineData(4, new byte[] { 0x03, 0x02, 0x01, 0x00, 0x07, 0x06, 0x05, 0x04, 0x08, 0x9 })]
        [InlineData(8, new byte[] { 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00, 0x08, 0x9 })]
        public void CanConvertEndiannessGeneric(int bytesOfType, byte[] input)
        {
            // Arrange
            var expected = Enumerable.Range(0x0, 0x9)
                .Select(value => (byte)value)
                .ToArray();

            // Act
            var actual = new byte[expected.Length];
            EndiannessConverterGeneric.Convert(bytesOfType, input, actual);

            // Assert
            Assert.True(actual.SequenceEqual(expected));
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
        public void CanConvertEndiannessAvx2<T>(T dummy, int length) where T : struct
        {
            // Arrange
            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var actual_converted = new byte[expected.Length];
            EndiannessConverterGeneric.Convert(bytesOfType, expected, actual_converted);

            // Act
            var actual = new byte[actual_converted.Length];
            EndiannessConverterAvx2.Convert(bytesOfType, actual_converted, actual);

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void ConvertEndiannessPerformanceTest()
        {
            // Arrange
            var length = 10_000_000;
            var bytesOfType = Unsafe.SizeOf<int>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var actual_converted = new byte[expected.Length];
            EndiannessConverterGeneric.Convert(bytesOfType, expected, actual_converted);

            // Act

            /* generic */
            var actual = new byte[actual_converted.Length];
            var sw = Stopwatch.StartNew();
            EndiannessConverterGeneric.Convert(bytesOfType, actual_converted, actual);
            var generic = sw.Elapsed.TotalMilliseconds;
            _logger.WriteLine($"Generic: {generic:F1} ms");

            ///* SSE2 */
            //if (Sse2.IsSupported)
            //{
            //    sw = Stopwatch.StartNew();
            //    actual = new byte[actual_converted.Length];
            //    EndiannessConverterSse2.Unshuffle(bytesOfType, actual_converted, actual);
            //    var sse2 = sw.Elapsed.TotalMilliseconds;
            //    _logger.WriteLine($"Generic: {sse2:F1} ms");
            //}

            /* AVX2 */
            if (Avx2.IsSupported)
            {
                sw = Stopwatch.StartNew();
                actual = new byte[actual_converted.Length];
                EndiannessConverterAvx2.Convert(bytesOfType, actual_converted, actual);
                var avx2 = sw.Elapsed.TotalMilliseconds;
                _logger.WriteLine($"AVX2: {avx2:F1} ms");
            }
        }
    }
}