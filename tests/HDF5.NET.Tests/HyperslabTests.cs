using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests.Reading
{
    public class HyperslabTests
    {
        // https://support.hdfgroup.org/HDF5/doc/H5.user/Chunking.html
        // https://support.hdfgroup.org/HDF5/doc/Advanced/Chunking/

        // Restrictions:
        // - The maximum number of elements in a chunk is 232 - 1 which is equal to 4,294,967,295.
        // - The maximum size for any chunk is 4GB.
        // - The size of a chunk cannot exceed the size of a fixed-size dataset. For example, a 
        //   dataset consisting of a 5x4 fixed-size array cannot be defined with 10x10 chunks.

        private readonly ITestOutputHelper _logger;

        public HyperslabTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }


        [Theory]
        // Dataset:
        // _________________________________________________________________________
        // |                                                                       |
        // |  0   1   2   3   4     5   6   7   9   9    10  11  12  13  14    15  |
        // |_______________________________________________________________________|
        //
        // Chunks:
        // _________________________________________________________________________________________
        // |       Chunk 0       |       Chunk 1       |       Chunk 2       |       Chunk 3       |
        // |  0   1   2   3   4  |  0   1   2   3   4  |  0   1   2   3   4  |  0   1   2   3   4  |
        // |_____________________|_____________________|_____________________|_____________________|

        [InlineData(0, 6, 4, 3)]
        // 1. Test: start = 6, count = 4, stride = 3
        // _________________________________________________________________________________________
        // |                     |                     |                     |                     |
        // |  _   _   _   _   _  |  _ | x   _   _   x  |  _   _   x   _   _  |  x   _   _ | _   _  |
        // |_____________________|_____________________|_____________________|_____________________|

        [InlineData(1, 7, 3, 3)]
        // 2. Test: start = 7, count = 3, stride = 3
        // _________________________________________________________________________________________
        // |                     |                     |                     |                     |
        // |  _   _   _   _   _  |  _   _ | x   _   _  |  x   _   _   x   _  |  _ | _   _   _   _  |
        // |_____________________|_____________________|_____________________|_____________________|

        [InlineData(2, 3, 2, 12)]
        // 2. Test: start = 3, count = 2, stride = 12
        // _________________________________________________________________________________________
        // |                     |                     |                     |                     |
        // |  _   _   _ | x   _  |  _   _   _   _   _  |  _   _   _   _   _  |  x | _   _   _   _  |
        // |_____________________|_____________________|_____________________|_____________________|

        [InlineData(3, 7, 4, 3)]
        // 3. Test: start = 7, count = 4, stride = 3
        // _________________________________________________________________________________________
        // |                     |                     |                     |                     |
        // |  _   _   _   _   _  |  _   _ | x   _   _  |  x   _   _   x   _  |  _   x   _   _ | _  |
        // |_____________________|_____________________|_____________________|_____________________|
        public void CanCalulateProjectionsSmall1D(int id, ulong start, ulong count, ulong stride)
        {
            // Arrange
            var datasetDims = new ulong[] { 16 };
            var chunkDims = new ulong[] { 5 };
            var memoryDims = new ulong[] { 16 };

            var datasetSlices = new Slice[]
            {
                Slice.Create(start, count, stride)
            };

            var memorySlices = new Slice[]
            {
                Slice.Create(start: 5, count, stride: 2)
            };

            var settings = new HyperslabSettings(Rank: datasetSlices.Count(), datasetDims, chunkDims, memoryDims);
            var hyperslabber = new Hyperslabber();

            SliceProjectionResult[] expected = null;

            if (id == 0)
            {
                expected = new SliceProjectionResult[]
                {
                    new SliceProjectionResult(Dimension: 0, new SliceProjection[]
                    {
                        new SliceProjection(ChunkIndex: 1, ChunkSlice: new Slice(Start: 1, Stop: 5, Stride: 3), MemorySlice: new Slice(Start: 0, Stop: 0, Stride: 0)),
                        new SliceProjection(ChunkIndex: 2, ChunkSlice: new Slice(Start: 2, Stop: 3, Stride: 3), MemorySlice: new Slice(Start: 0, Stop: 0, Stride: 0)),
                        new SliceProjection(ChunkIndex: 3, ChunkSlice: new Slice(Start: 0, Stop: 1, Stride: 3), MemorySlice: new Slice(Start: 0, Stop: 0, Stride: 0))
                    })
                };
            }
            else if (id == 1)
            {
                expected = new SliceProjectionResult[]
                {
                    new SliceProjectionResult(Dimension: 0, new SliceProjection[]
                    {
                        new SliceProjection(ChunkIndex: 1, ChunkSlice: new Slice(Start: 2, Stop: 3, Stride: 3), MemorySlice: new Slice(Start: 0, Stop: 0, Stride: 0)),
                        new SliceProjection(ChunkIndex: 2, ChunkSlice: new Slice(Start: 0, Stop: 4, Stride: 3), MemorySlice: new Slice(Start: 0, Stop: 0, Stride: 0))
                    })
                };
            }
            else if (id == 2)
            {
                expected = new SliceProjectionResult[]
                {
                    new SliceProjectionResult(Dimension: 0, new SliceProjection[]
                    {
                        new SliceProjection(ChunkIndex: 0, ChunkSlice: new Slice(Start: 3, Stop: 4, Stride: 12), MemorySlice: new Slice(Start: 0, Stop: 0, Stride: 0)),
                        new SliceProjection(ChunkIndex: 3, ChunkSlice: new Slice(Start: 0, Stop: 1, Stride: 12), MemorySlice: new Slice(Start: 0, Stop: 0, Stride: 0))
                    })
                };
            }

            // Act
            Func<SliceProjectionResult[]> func = () => hyperslabber.ComputeSliceProjections(datasetSlices, memorySlices, settings);

            // Assert
            if (expected is not null)
            {
                var actual = func();
                Assert.Equal(actual[0].Dimension, expected[0].Dimension);
                Assert.True(actual[0].SliceProjections.SequenceEqual(expected[0].SliceProjections));
            }
            else
            {
                Assert.Throws<Exception>(func);
            }
        }

        [Fact]
        /*   DS [2 x 3 x 6]                        CHUNK [2 x 3 x 6]
         *                  |-05--11--17-|                        |<----------->|
         *               |-04--10--16-|                        |<----------->|
         *            |-03--09--15-|                        |<----------->|
         *         |-02--08--14-|                        |<----------->|
         *      |-01--07--13-|                        |<----------->|
         *   |-00--06--12-|                        |<-----01---->|
         *   |-18--24--30-|                        |<----------->|
         */

        /*                                         CHUNK [1 x 2 x 3]
         *                                                        |<------><----|
         *                                                     |<------><----|
         *                                                  |<--02--><-04-|
         *                                               |<------><----|
         *                                            |<------><----|
         *                                         |<--01--><-03-|
         *                                         |<--05--><-07-|
         */
        //public void CanCalulateProjectionsSmall3D()
        //{
        //    // Arrange
        //    var datasetDims = new ulong[] { 2, 3, 6 };
        //    var chunkDims = new ulong[] { 1, 2, 3 };

        //    var slices = new Slice[]
        //    {
        //        new Slice(Start: 0, Stop: 1, Stride: 1),
        //        new Slice(Start: 1, Stop: 3, Stride: 1),
        //        new Slice(Start: 1, Stop: 4, Stride: 1)
        //    };

        //    var settings = new HyperslabSettings(Rank: slices.Count(), datasetDims, chunkDims);
        //    var hyperslabber = new Hyperslabber();

        //    var expected = new SliceProjectionResult[]
        //    {
        //        new SliceProjectionResult(Dimension: 0, new SliceProjection[]
        //        {
        //            new SliceProjection(ChunkIndex: 0, ChunkSlice: new Slice(Start: 0, Stop: 1, Stride: 1), MemorySlice: new Slice(Start: 0, Stop: 1, Stride: 1))
        //        }),
        //        new SliceProjectionResult(Dimension: 1, new SliceProjection[]
        //        {
        //            new SliceProjection(ChunkIndex: 0, ChunkSlice: new Slice(Start: 1, Stop: 2, Stride: 1), MemorySlice: new Slice(Start: 0, Stop: 1, Stride: 1)),
        //            new SliceProjection(ChunkIndex: 1, ChunkSlice: new Slice(Start: 0, Stop: 1, Stride: 1), MemorySlice: new Slice(Start: 1, Stop: 2, Stride: 1))
        //        }),
        //        new SliceProjectionResult(Dimension: 2, new SliceProjection[]
        //        {
        //            new SliceProjection(ChunkIndex: 0, ChunkSlice: new Slice(Start: 1, Stop: 3, Stride: 1), MemorySlice: new Slice(Start: 0, Stop: 2, Stride: 1)),
        //            new SliceProjection(ChunkIndex: 1, ChunkSlice: new Slice(Start: 0, Stop: 1, Stride: 1), MemorySlice: new Slice(Start: 2, Stop: 3, Stride: 1))
        //        })
        //    };

        //    // Act
        //    var actual = hyperslabber.ComputeSliceProjections(settings, slices);

        //    // Assert
        //    Assert.Equal(actual[0].Dimension, expected[0].Dimension);
        //    Assert.Equal(actual[1].Dimension, expected[1].Dimension);
        //    Assert.Equal(actual[2].Dimension, expected[2].Dimension);

        //    Assert.True(actual[0].SliceProjections.SequenceEqual(expected[0].SliceProjections));
        //    Assert.True(actual[1].SliceProjections.SequenceEqual(expected[1].SliceProjections));
        //    Assert.True(actual[2].SliceProjections.SequenceEqual(expected[2].SliceProjections));

        //    //// source
        //    //var sourceBuffer1 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s1 = MemoryMarshal.Cast<byte, int>(sourceBuffer1);
        //    //s1[0] = 0; s1[1] = 1; s1[2] = 2; s1[3] = 6; s1[4] = 7; s1[5] = 8;

        //    //var sourceBuffer2 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s2 = MemoryMarshal.Cast<byte, int>(sourceBuffer2);
        //    //s2[0] = 3; s2[1] = 4; s2[2] = 5; s2[3] = 9; s2[4] = 10; s2[5] = 11;

        //    //var sourceBuffer3 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s3 = MemoryMarshal.Cast<byte, int>(sourceBuffer3);
        //    //s3[0] = 12; s3[1] = 13; s3[2] = 14; s3[3] = 0; s3[4] = 0; s3[5] = 0;

        //    //var sourceBuffer4 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s4 = MemoryMarshal.Cast<byte, int>(sourceBuffer4);
        //    //s4[0] = 15; s4[1] = 16; s4[2] = 17; s4[3] = 0; s4[4] = 0; s4[5] = 0;

        //    //var sourceBuffer5 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s5 = MemoryMarshal.Cast<byte, int>(sourceBuffer5);
        //    //s5[0] = 18; s5[1] = 19; s5[2] = 20; s5[3] = 24; s5[4] = 25; s5[5] = 26;

        //    //var sourceBuffer6 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s6 = MemoryMarshal.Cast<byte, int>(sourceBuffer6);
        //    //s6[0] = 21; s6[1] = 22; s6[2] = 23; s6[3] = 27; s6[4] = 28; s6[5] = 29;

        //    //var sourceBuffer7 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s7 = MemoryMarshal.Cast<byte, int>(sourceBuffer7);
        //    //s7[0] = 30; s7[1] = 31; s7[2] = 32; s7[3] = 0; s7[4] = 0; s7[5] = 0;

        //    //var sourceBuffer8 = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var s8 = MemoryMarshal.Cast<byte, int>(sourceBuffer8);
        //    //s8[0] = 33; s8[1] = 34; s8[2] = 35; s8[3] = 0; s8[4] = 0; s8[5] = 0;

        //    //var chunks = new Memory<byte>[]
        //    //{
        //    //    sourceBuffer1,
        //    //    sourceBuffer2,
        //    //    sourceBuffer3,
        //    //    sourceBuffer4,
        //    //    sourceBuffer5,
        //    //    sourceBuffer6,
        //    //    sourceBuffer7,
        //    //    sourceBuffer8,
        //    //};

        //    //// target
        //    //var targetBuffer = new byte[1 * 2 * 3 * sizeof(int)];
        //    //var target = new Memory<byte>(targetBuffer);

        //    //var normalizedDatasetDims = new ulong[3];

        //    //for (ulong i = 0; i < 3; i++)
        //    //{
        //    //    normalizedDatasetDims[i] = H5Utils.CeilDiv(datasetDims[i], chunkDims[i]);
        //    //}

        //    //// copyinfo
        //    //var copyInfo = new CopyInfo(
        //    //    Rank: 3,
        //    //    NormalizedDatasetDims: normalizedDatasetDims,
        //    //    ChunkDims: chunkDims,
        //    //    SliceProjectionResults: actual,
        //    //    TypeSize: sizeof(int),
        //    //    Chunks: chunks,
        //    //    Target: target
        //    //);

        //    //HyperslabUtils.Copy(copyInfo);
        //    //var result = MemoryMarshal.Cast<byte, int>(target.Span);
        //}

        //[Fact]
        //public void CanCalulateProjectionsLarge()
        //{
        //    // Arrange
        //    var datasetDims = new ulong[] { 20, 30, 40 };
        //    var chunkDims = new ulong[] { 6, 6, 6 };

        //    var slices = new Slice[]
        //    {
        //        new Slice { Start = 10, Stop = 20, Stride = 3 },
        //        new Slice { Start = 11, Stop = 21, Stride = 4 },
        //        new Slice { Start = 25, Stop = 39, Stride = 1 }
        //    };

        //    var settings = new HyperslabSettings(Rank: slices.Count(), datasetDims, chunkDims);
        //    var hyperslabber = new Hyperslabber();

        //    var expected = new SliceProjectionResult[]
        //    {
        //        new SliceProjectionResult(Dimension: 0, new SliceProjection[]
        //            {
        //                new SliceProjection() { ChunkIndex = 1, ChunkSlice = new Slice() { Start = 4, Stop = 6, Stride = 3 }, MemorySlice = new Slice() { Start = 0, Stop = 1, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 2, ChunkSlice = new Slice() { Start = 1, Stop = 6, Stride = 3 }, MemorySlice = new Slice() { Start = 1, Stop = 3, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 3, ChunkSlice = new Slice() { Start = 1, Stop = 2, Stride = 3 }, MemorySlice = new Slice() { Start = 3, Stop = 4, Stride = 1 } }
        //            }),
        //        new SliceProjectionResult(Dimension: 1, new SliceProjection[]
        //            {
        //                new SliceProjection() { ChunkIndex = 1, ChunkSlice = new Slice() { Start = 5, Stop = 6, Stride = 4 }, MemorySlice = new Slice() { Start = 0, Stop = 1, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 2, ChunkSlice = new Slice() { Start = 3, Stop = 6, Stride = 4 }, MemorySlice = new Slice() { Start = 1, Stop = 2, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 3, ChunkSlice = new Slice() { Start = 1, Stop = 3, Stride = 4 }, MemorySlice = new Slice() { Start = 2, Stop = 3, Stride = 1 } }
        //            }),
        //        new SliceProjectionResult(Dimension: 2, new SliceProjection[]
        //            {
        //                new SliceProjection() { ChunkIndex = 4, ChunkSlice = new Slice() { Start = 1, Stop = 6, Stride = 1 }, MemorySlice = new Slice() { Start = 0, Stop = 5, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 5, ChunkSlice = new Slice() { Start = 0, Stop = 6, Stride = 1 }, MemorySlice = new Slice() { Start = 5, Stop = 11, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 6, ChunkSlice = new Slice() { Start = 0, Stop = 3, Stride = 1 }, MemorySlice = new Slice() { Start = 11, Stop = 14, Stride = 1 } }
        //            })
        //    };

        //    // Act
        //    var actual = hyperslabber.ComputeSliceProjections(settings, slices);

        //    // Assert
        //    Assert.Equal(actual[0].Dimension, expected[0].Dimension);
        //    Assert.Equal(actual[1].Dimension, expected[1].Dimension);
        //    Assert.Equal(actual[2].Dimension, expected[2].Dimension);

        //    Assert.True(actual[0].SliceProjections.SequenceEqual(expected[0].SliceProjections));
        //    Assert.True(actual[1].SliceProjections.SequenceEqual(expected[1].SliceProjections));
        //    Assert.True(actual[2].SliceProjections.SequenceEqual(expected[2].SliceProjections));
        //}

        //[Fact]
        //public void CanCalulateProjectionsLargeWithSkippedChunks()
        //{
        //    // Arrange
        //    var datasetDims = new ulong[] { 50, 30, 70 };
        //    var chunkDims = new ulong[] { 6, 6, 6 };

        //    var slices = new Slice[]
        //    {
        //        new Slice { Start = 10, Stop = 40, Stride = 15 },
        //        new Slice { Start = 11, Stop = 21, Stride = 3 },
        //        new Slice { Start = 25, Stop = 60, Stride = 15 }
        //    };

        //    var settings = new HyperslabSettings(Rank: slices.Count(), datasetDims, chunkDims);
        //    var hyperslabber = new Hyperslabber();

        //    var expected = new SliceProjectionResult[]
        //    {
        //        new SliceProjectionResult(Dimension: 0, new SliceProjection[]
        //            {
        //                new SliceProjection() { ChunkIndex = 1, ChunkSlice = new Slice() { Start = 4, Stop = 6, Stride = 15 }, MemorySlice = new Slice() { Start = 0, Stop = 1, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 4, ChunkSlice = new Slice() { Start = 1, Stop = 6, Stride = 15 }, MemorySlice = new Slice() { Start = 1, Stop = 2, Stride = 1 } }
        //            }),
        //        new SliceProjectionResult(Dimension: 1, new SliceProjection[]
        //            {
        //                new SliceProjection() { ChunkIndex = 1, ChunkSlice = new Slice() { Start = 5, Stop = 6, Stride = 3 }, MemorySlice = new Slice() { Start = 0, Stop = 1, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 2, ChunkSlice = new Slice() { Start = 2, Stop = 6, Stride = 3 }, MemorySlice = new Slice() { Start = 1, Stop = 3, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 3, ChunkSlice = new Slice() { Start = 2, Stop = 3, Stride = 3 }, MemorySlice = new Slice() { Start = 3, Stop = 4, Stride = 1 } }
        //            }),
        //        new SliceProjectionResult(Dimension: 2, new SliceProjection[]
        //            {
        //                new SliceProjection() { ChunkIndex = 4, ChunkSlice = new Slice() { Start = 1, Stop = 6, Stride = 15 }, MemorySlice = new Slice() { Start = 0, Stop = 1, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 6, ChunkSlice = new Slice() { Start = 4, Stop = 6, Stride = 15 }, MemorySlice = new Slice() { Start = 1, Stop = 2, Stride = 1 } },
        //                new SliceProjection() { ChunkIndex = 9, ChunkSlice = new Slice() { Start = 1, Stop = 6, Stride = 15 }, MemorySlice = new Slice() { Start = 2, Stop = 3, Stride = 1 } }
        //            })
        //    };

        //    // Act
        //    var actual = hyperslabber.ComputeSliceProjections(settings, slices);

        //    // Assert
        //    Assert.Equal(actual[0].Dimension, expected[0].Dimension);
        //    Assert.Equal(actual[1].Dimension, expected[1].Dimension);
        //    Assert.Equal(actual[2].Dimension, expected[2].Dimension);

        //    Assert.True(actual[0].SliceProjections.SequenceEqual(expected[0].SliceProjections));
        //    Assert.True(actual[1].SliceProjections.SequenceEqual(expected[1].SliceProjections));
        //    Assert.True(actual[2].SliceProjections.SequenceEqual(expected[2].SliceProjections));
        //}

        //[Fact]
        //public void CanReadHyperslab()
        //{
        //    TestUtils.RunForAllVersions(version =>
        //    {
        //        // Arrange
        //        var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDatasetForHyperslab(fileId));

        //        // Act
        //        using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
        //        var parent = root.Group("chunked");
        //        var dataset = parent.Dataset("hyperslab");

        //        foreach (var attribute in attributes)
        //        {
        //            var actual = attribute.ReadCompound<TestStructL1>();

        //            // Assert
        //            Assert.True(actual.SequenceEqual(TestData.NonNullableStructData));
        //        }

        //        Assert.Equal(expectedCount, attributes.Count);
        //    });
        //}
    }
}