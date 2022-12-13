using HDF.PInvoke;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif
using Xunit;
using Xunit.Abstractions;

#warning Add test with additional filter (shuffle) to detect wrongly returned filter sizes

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

        [InlineData("minbits_0", (byte)24, 24, 24, 24, 24, 24)]
        [InlineData("minbits_full", (byte)1, 0, 255, 1, 0, 255)]

        [InlineData("uint8_nofill", (byte)0, 24, 5, 12, 13, 0)]
        [InlineData("uint16_nofill", (ushort)0, 6144, 1280, 3072, 3328, 0)]
        [InlineData("uint32_nofill", (uint)0, 402653184, 83886080, 201326592, 218103808, 0)]
        [InlineData("uint64_nofill", (ulong)0, 1729382256910270464, 360287970189639680, 864691128455135232, 936748722493063168, 0)]

        [InlineData("uint8_fill", (byte)127, 24, 5, 12, 13, 127)]
        [InlineData("uint16_fill", (ushort)32767, 6144, 1280, 3072, 3328, 32767)]
        [InlineData("uint32_fill", (uint)2147483647, 402653184, 83886080, 201326592, 218103808, 2147483647)]
        [InlineData("uint64_fill", (ulong)9223372036854775808, 1729382256910270464, 360287970189639680, 864691128455135232, 936748722493063168, 9223372036854775808)]

        [InlineData("int8_nofill", (sbyte)0, 24, -5, 12, 13, 0)]
        [InlineData("int16_nofill", (short)0, 6144, -1280, 3072, 3328, 0)]
        [InlineData("int32_nofill", (int)0, 402653184, -83886080, 201326592, 218103808, 0)]
        [InlineData("int64_nofill", (long)0, 1729382256910270464, -360287970189639680, 864691128455135232, 936748722493063168, 0)]

        [InlineData("int8_fill", (sbyte)63, 24, -5, 12, 13, 63)]
        [InlineData("int16_fill", (short)16383, 6144, -1280, 3072, 3328, 16383)]
        [InlineData("int32_fill", (int)1073741823, 402653184, -83886080, 201326592, 218103808, 1073741823)]
        [InlineData("int64_fill", (long)4611686018427387904, 1729382256910270464, -360287970189639680, 864691128455135232, 936748722493063168, 4611686018427387904)]

        [InlineData("float32_nofill", (float)0, 24.7, -5.3, 12.2, 13.2, 0)]
        [InlineData("float64_nofill", (double)0, 24.7, -5.3, 12.2, 13.2, 0)]

        [InlineData("float32_fill", (float)99.9, 24.7, -5.3, 12.2, 13.2, 99.9)]
        [InlineData("float64_fill", (double)99.9, 24.7, -5.3, 12.2, 13.2, 99.9)]
        public void CanDefilterScaleOffset<T>(string datasetName, T e1, T e2, T e3, T e4, T e5, T e6)
            where T : unmanaged
        {
            // import h5py

            // with h5py.File("scaleoffset.h5", "w") as f:
            
            //     # minbits = 0
            //     size = 8 << 0
            //     data = [24, 24, 24, 24, 24, 24]
            //     dataset = f.create_dataset(f"minbits_0", (6,), chunks=(3,), dtype=f"<u{size/8:.0f}", scaleoffset=True)
            //     dataset[0:6] = data

            //     # minbits = full
            //     size = 8 << 0
            //     data = [1, 0, 255, 1, 0, 255]
            //     dataset = f.create_dataset(f"minbits_full", (6,), chunks=(3,), dtype=f"<u{size/8:.0f}", scaleoffset=True)
            //     dataset[0:6] = data

            //     # unsigned integer, no fill value
            //     for shiftFactor in range(0, 4):
            //         size = 8 << shiftFactor
            //         data = [24 << (size - 8), 5 << (size - 8), 12 << (size - 8), 13 << (size - 8)]
            //         dataset = f.create_dataset(f"uint{size}_nofill", (6,), chunks=(3,), dtype=f"<u{size/8:.0f}", scaleoffset=True)
            //         dataset[1:5] = data

            //     # unsigned integer, fill value
            //     for shiftFactor in range(0, 4):
            //         size = 8 << shiftFactor
            //         max = pow(2, size) - 1
            //         data = [24 << (size - 8), 5 << (size - 8), 12 << (size - 8), 13 << (size - 8)]
            //         dataset = f.create_dataset(f"uint{size}_fill", (6,), chunks=(3,), dtype=f"<u{size/8:.0f}", scaleoffset=True, fillvalue=max/2)
            //         dataset[1:5] = data

            //     # signed integer, no fill value
            //     for shiftFactor in range(0, 4):
            //         size = 8 << shiftFactor
            //         data = [24 << (size - 8), -5 << (size - 8), 12 << (size - 8), 13 << (size - 8)]
            //         dataset = f.create_dataset(f"int{size}_nofill", (6,), chunks=(3,), dtype=f"<i{size/8:.0f}", scaleoffset=True)
            //         dataset[1:5] = data

            //     # signed integer, fill value
            //     for shiftFactor in range(0, 4):
            //         size = 8 << shiftFactor
            //         max = pow(2, size - 1) - 1
            //         data = [24 << (size - 8), -5 << (size - 8), 12 << (size - 8), 13 << (size - 8)]
            //         dataset = f.create_dataset(f"int{size}_fill", (6,), chunks=(3,), dtype=f"<i{size/8:.0f}", scaleoffset=True, fillvalue=max/2)
            //         dataset[1:5] = data

            //     # float, no fill value
            //     size = 32
            //     data = [24.7, -5.3, 12.2, 13.2]
            //     dataset = f.create_dataset(f"float{size}_nofill", (6,), chunks=(3,), dtype=f"<f{size/8:.0f}", scaleoffset=2)
            //     dataset[1:5] = data

            //     size = 64
            //     data = [24.7, -5.3, 12.2, 13.2]
            //     dataset = f.create_dataset(f"float{size}_nofill", (6,), chunks=(3,), dtype=f"<f{size/8:.0f}", scaleoffset=2)
            //     dataset[1:5] = data

            //     # float, fill value
            //     size = 32
            //     data = [24.7, -5.3, 12.2, 13.2]
            //     dataset = f.create_dataset(f"float{size}_fill", (6,), chunks=(3,), dtype=f"<f{size/8:.0f}", scaleoffset=2, fillvalue=99.9)
            //     dataset[1:5] = data

            //     size = 64
            //     data = [24.7, -5.3, 12.2, 13.2]
            //     dataset = f.create_dataset(f"float{size}_fill", (6,), chunks=(3,), dtype=f"<f{size/8:.0f}", scaleoffset=2, fillvalue=99.9)
            //     dataset[1:5] = data

            // Arrange
            var expected = new T[] { e1, e2, e3, e4, e5, e6 };
            var filePath = "./testfiles/scaleoffset.h5";

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dataset = root.Dataset(datasetName);

            var actual = dataset.Read<T>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Theory]
        [InlineData("blosclz", true)]
        [InlineData("lz4", true)]
        [InlineData("lz4hc", true)]
        [InlineData("snappy", false)]
        [InlineData("zlib", true)]
        [InlineData("zstd", true)]
        [InlineData("blosclz_bit", true)]
        public void CanDefilterBlosc2(string datasetName, bool shouldSucceed)
        {
            // # https://github.com/silx-kit/hdf5plugin
            // import h5py
            // import hdf5plugin
            //
            // data = list(range(0, 1000))
            //
            // with h5py.File('blosc.h5', 'w') as f:
            //     f.create_dataset('blosclz',      data=data, **hdf5plugin.Blosc(cname='blosclz', clevel=9, shuffle=hdf5plugin.Blosc.SHUFFLE))
            //     f.create_dataset('lz4',          data=data, **hdf5plugin.Blosc(cname='lz4', clevel=9, shuffle=hdf5plugin.Blosc.SHUFFLE))
            //     f.create_dataset('lz4hc',        data=data, **hdf5plugin.Blosc(cname='lz4hc', clevel=9, shuffle=hdf5plugin.Blosc.SHUFFLE))
            //     f.create_dataset('snappy',       data=data, **hdf5plugin.Blosc(cname='snappy', clevel=9, shuffle=hdf5plugin.Blosc.SHUFFLE))
            //     f.create_dataset('zlib',         data=data, **hdf5plugin.Blosc(cname='zlib', clevel=9, shuffle=hdf5plugin.Blosc.SHUFFLE))
            //     f.create_dataset('zstd',         data=data, **hdf5plugin.Blosc(cname='zstd', clevel=9, shuffle=hdf5plugin.Blosc.SHUFFLE))
            //     f.create_dataset('blosclz_bit',  data=data, **hdf5plugin.Blosc(cname='zlib', clevel=9, shuffle=hdf5plugin.Blosc.BITSHUFFLE))

            // Arrange
            var filePath = "./testfiles/blosc.h5";
            var expected = Enumerable.Range(0, 1000).ToArray();

            H5Filter.Register(identifier: (H5FilterID)32001, name: "blosc2", filterFunc: BloscHelper.FilterFunc);

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dataset = root.Dataset(datasetName);

            if (shouldSucceed)
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
        public void CanDefilterBZip2()
        {
            // # Works only with Linux! On Windows, deflate is used instead.
            // import numpy
            // import tables


            // fileName = 'bzip2.h5'
            // shape = (1000,)
            // atom = tables.Int32Atom()
            // filters = tables.Filters(complevel=9, complib='bzip2')

            // with tables.open_file(fileName, 'w') as f:
            //     dataset = f.create_carray(f.root, 'bzip2', atom, shape, filters=filters)
            //     dataset[:] = list(range(0, 1000))

            // Arrange
            var filePath = "./testfiles/bzip2.h5";
            var expected = Enumerable.Range(0, 1000).ToArray();

            H5Filter.Register(identifier: (H5FilterID)307, name: "bzip2", filterFunc: BZip2Helper.FilterFunc);

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dataset = root.Dataset("bzip2");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanDefilterFletcher()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddFilteredDataset_Fletcher(fileId));

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset("fletcher");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestData.MediumData));
        }

        [Theory]
        [InlineData("MicrosoftDeflateStream")]
        [InlineData("SharpZipLibInflater")]
#if NET5_0_OR_GREATER
        // https://iobservable.net/blog/2013/08/06/clr-limitations/
        // "It seems that the maximum array base element size is limited to 64KB."
        [InlineData("Intel_ISA_L_Inflate")]
#endif
        public void CanDefilterZLib(string filterFuncId)
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddFilteredDataset_ZLib(fileId));

            FilterFunc func = filterFuncId switch
            {
                "MicrosoftDeflateStream" => null, /* default */
                "SharpZipLibInflater" => DeflateHelper_SharpZipLib.FilterFunc,
                "Intel_ISA_L_Inflate" => DeflateHelper_Intel_ISA_L.FilterFunc,
                _ => throw new NotSupportedException($"The filter func ID {filterFuncId} is not supported.")
            };

            if (func is not null)
                H5Filter.Register(H5FilterID.Deflate, "deflate", func);

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset("multi");
            var actual = dataset.Read<int>();

            // Assert
            Assert.True(actual.SequenceEqual(TestData.MediumData));
        }

//#error 16 byte and arbitrary number of bytes tests missing
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
        public void CanUnshuffleGeneric<T>(T dummy, int length)
            where T : unmanaged
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(null, skipShuffle: true);

            // Act
            var actual = new byte[actual_shuffled.Length];
            ShuffleGeneric.Unshuffle(bytesOfType, actual_shuffled, actual);

            // Assert
            Assert.True(actual.AsSpan().SequenceEqual(expected));
        }

#if NET5_0_OR_GREATER
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
        public void CanUnshuffleAvx2<T>(T dummy, int length) 
            where T : unmanaged
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(default, skipShuffle: true);

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
        public void CanUnshuffleSse2<T>(T dummy, int length) 
            where T : unmanaged
        {
            // Arrange
            var version = H5F.libver_t.LATEST;

            var bytesOfType = Unsafe.SizeOf<T>();
            var expected = Enumerable.Range(0, length * bytesOfType)
                .Select(value => unchecked((byte)value)).ToArray();

            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            TestUtils.AddFilteredDataset_Shuffle(fileId, bytesOfType: bytesOfType, length, expected));

            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(default, skipShuffle: true);

            // Act
            var actual = new byte[actual_shuffled.Length];
            ShuffleAvx2.Unshuffle(bytesOfType, actual_shuffled, actual);

            // Assert
            Assert.True(actual.AsSpan().SequenceEqual(expected));
        }
#endif

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

            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("filtered");
            var dataset = parent.Dataset($"shuffle_{bytesOfType}");
            var actual_shuffled = dataset.Read<byte>(default, skipShuffle: true);

            // Act

            /* generic */
            var sw = Stopwatch.StartNew();
            var actual = new byte[actual_shuffled.Length];
            ShuffleGeneric.Unshuffle(bytesOfType, actual_shuffled, actual);
            var generic = sw.Elapsed.TotalMilliseconds;
            _logger.WriteLine($"Generic: {generic:F1} ms");

#if NET5_0_OR_GREATER
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
#endif
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

#if NET5_0_OR_GREATER
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
        public void CanConvertEndiannessAvx2<T>(T dummy, int length)
            where T : unmanaged
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
#endif

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

#if NET5_0_OR_GREATER
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
#endif
        }


        [Fact]
        public void CanReadBigEndian()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId =>
            {
                TestUtils.AddSmall(fileId, ContainerType.Attribute);
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
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);

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