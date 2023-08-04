using System.Runtime.InteropServices;
using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public class SelectionTests
    {
        // https://support.hdfgroup.org/HDF5/doc/H5.user/Chunking.html
        // "The write will fail because the selection goes beyond the extent of the dimension" (https://support.hdfgroup.org/HDF5/Tutor/selectsimple.html)
        // -> hyperslab's actual points are not allowed to exceed the extend of the dimension

        // Restrictions:
        // - The maximum number of elements in a chunk is 2^32 - 1 which is equal to 4,294,967,295.
        // - The maximum size for any chunk is 4GB.
        // - The size of a chunk cannot exceed the size of a fixed-size dataset. For example, a 
        //   dataset consisting of a 5x4 fixed-size array cannot be defined with 10x10 chunks.
        // https://support.hdfgroup.org/images/tutr-subset2.png

        private static void Converter(Memory<byte> source, Memory<int> target) 
            => source.Span.CopyTo(MemoryMarshal.AsBytes(target.Span));

        [Fact]
        public void CanWalk_NoneSelection()
        {
            // Arrange
            var dims = new ulong[] { 14, 14, 14 };
            var chunkDims = new ulong[] { 3, 3, 3 };
            var selection = new NoneSelection();

            // Act
            var actual = SelectionUtils
                .Walk(rank: 3, dims, chunkDims, selection)
                .ToArray();

            // Assert
            Assert.Empty(actual);
        }

        [Fact]
        /*    /                                         /
         *   /  Dataset dimensions:  [14, 14, 14]      / 14
         *  /     Chunk dimensions:  [3, 3, 3]        /
         * /_  _  _  _  _  _  _  _  _  _  _  _  _  _ /
         * |X      |       X|        |        |     |
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     |
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  X  _| _  _  _| _  _|
         * |       |        |        |        |     | 14
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     |
         * |       |        |        |        |     |   /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|  /
         * |   X   |        |        |        |     | /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|/
         *                    14
         */
        public void CanWalk_DelegateSelection()
        {
            // Arrange
            var dims = new ulong[] { 14, 14, 14 };
            var chunkDims = new ulong[] { 3, 3, 3 };

            static IEnumerable<Step> Walker(ulong[] datasetDimensions)
            {
                yield return new Step() { Coordinates = new ulong[] { 00, 00, 00 }, ElementCount = 1 };
                yield return new Step() { Coordinates = new ulong[] { 00, 05, 10 }, ElementCount = 1 };
                yield return new Step() { Coordinates = new ulong[] { 12, 01, 10 }, ElementCount = 1 };
                yield return new Step() { Coordinates = new ulong[] { 05, 07, 09 }, ElementCount = 1 };
            };

            var selection = new DelegateSelection(totalElementCount: 5, Walker);

            var expected = new RelativeStep[]
            {
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 0, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 3}, Offset = 7, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {4, 0, 3}, Offset = 4, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 3}, Offset = 21, Length = 1 },
            };

            // Act
            var actual = SelectionUtils
                .Walk(rank: 3, dims, chunkDims, selection)
                .ToArray();

            // Assert
            for (int i = 0; i < actual.Length; i++)
            {
                var actual_current = actual[i];
                var expected_current = expected[i];

                Assert.Equal(actual_current.Chunk[0], expected_current.Chunk[0]);
                Assert.Equal(actual_current.Chunk[1], expected_current.Chunk[1]);
                Assert.Equal(actual_current.Chunk[2], expected_current.Chunk[2]);
                Assert.Equal(actual_current.Offset, expected_current.Offset);
                Assert.Equal(actual_current.Length, expected_current.Length);
            }
        }

        [Fact]
        /*    /                                         /
         *   /  Dataset dimensions:  [14, 14, 14]      / 14
         *  /     Chunk dimensions:  [3, 3, 3]        /
         * /_  _  _  _  _  _  _  _  _  _  _  _  _  _ /
         * |X      |       X|        |        |     |
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     |
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  X  _| _  _  _| _  _|
         * |       |        |        |        |     | 14
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     |
         * |       |        |        |        |     |   /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|  /
         * |   X   |        |        |        |     | /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|/
         *                    14
         */
        public void CanWalk_PointSelection()
        {
            // Arrange
            var dims = new ulong[] { 14, 14, 14 };
            var chunkDims = new ulong[] { 3, 3, 3 };

            var selection = new PointSelection(new ulong[,] {
                { 00, 00, 00 },
                { 00, 05, 10 },
                { 12, 01, 10 },
                { 05, 07, 09 }
            });

            var expected = new RelativeStep[]
            {
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 0, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 3}, Offset = 7, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {4, 0, 3}, Offset = 4, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 3}, Offset = 21, Length = 1 },
            };

            // Act
            var actual = SelectionUtils
                .Walk(rank: 3, dims, chunkDims, selection)
                .ToArray();

            // Assert
            for (int i = 0; i < actual.Length; i++)
            {
                var actual_current = actual[i];
                var expected_current = expected[i];

                Assert.Equal(actual_current.Chunk[0], expected_current.Chunk[0]);
                Assert.Equal(actual_current.Chunk[1], expected_current.Chunk[1]);
                Assert.Equal(actual_current.Chunk[2], expected_current.Chunk[2]);
                Assert.Equal(actual_current.Offset, expected_current.Offset);
                Assert.Equal(actual_current.Length, expected_current.Length);
            }
        }

        [Fact]
        /*    /                                         /
         *   /  Dataset dimensions:  [14, 14, 14]      / 14
         *  /     Chunk dimensions:  [3, 3, 3]        /
         * /_  _  _  _  _  _  _  _  _  _  _  _  _  _ /
         * |       |        |        |        |     |
         * |       |        |        |        |     |
         * |_  _  _| X  X  _| _  _  X| X  _  _| _  _|
         * |       | X  X   |       X| X      |     |
         * |       | X  X   |       X| X      |     |
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     | 14
         * |       |        |        |        |     |
         * |_  _  _| X  X  _| _  _  X| X  _  _| _  _|
         * |       | X  X   |       X| X      |     |
         * |       | X  X   |       X| X      |     |   /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|  /
         * |       |        |        |        |     | /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|/
         *                    14
         */
        public void CanWalk_RegularHyperslab()
        {
            // Arrange
            var dims = new ulong[] { 14, 14, 14 };
            var chunkDims = new ulong[] { 3, 3, 3 };

            var selection = new HyperslabSelection(
                rank: 3,
                starts: new ulong[] { 2, 3, 2 },
                strides: new ulong[] { 6, 5, 6 },
                counts: new ulong[] { 2, 2, 2 },
                blocks: new ulong[] { 3, 2, 5 }
            );

            var expected = new RelativeStep[]
            {
                // row 0
                new RelativeStep() { Chunk = new ulong[] {0, 1, 0}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 1}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 2}, Offset = 18, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 2}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 3}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 4}, Offset = 18, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {0, 1, 0}, Offset = 23, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 1}, Offset = 21, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 2}, Offset = 21, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 2}, Offset = 23, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 3}, Offset = 21, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 4}, Offset = 21, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {0, 2, 0}, Offset = 26, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 2, 1}, Offset = 24, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 2, 2}, Offset = 24, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 2, 2}, Offset = 26, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 2, 3}, Offset = 24, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 2, 4}, Offset = 24, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {0, 3, 0}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 3, 1}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 3, 2}, Offset = 18, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 3, 2}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {0, 3, 3}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 3, 4}, Offset = 18, Length = 1 },

                // row 1
                new RelativeStep() { Chunk = new ulong[] {1, 1, 0}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 1}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 0, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 3}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 4}, Offset = 0, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {1, 1, 0}, Offset = 5, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 1}, Offset = 3, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 3, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 5, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 3}, Offset = 3, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 4}, Offset = 3, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {1, 2, 0}, Offset = 8, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 1}, Offset = 6, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 2}, Offset = 6, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 2}, Offset = 8, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 3}, Offset = 6, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 4}, Offset = 6, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {1, 3, 0}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 1}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 2}, Offset = 0, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 2}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 3}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 4}, Offset = 0, Length = 1 },

                // row 2
                new RelativeStep() { Chunk = new ulong[] {1, 1, 0}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 1}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 9, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 3}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 4}, Offset = 9, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {1, 1, 0}, Offset = 14, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 1}, Offset = 12, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 12, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 2}, Offset = 14, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 3}, Offset = 12, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 1, 4}, Offset = 12, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {1, 2, 0}, Offset = 17, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 1}, Offset = 15, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 2}, Offset = 15, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 2}, Offset = 17, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 3}, Offset = 15, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 2, 4}, Offset = 15, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {1, 3, 0}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 1}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 2}, Offset = 9, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 2}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 3}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {1, 3, 4}, Offset = 9, Length = 1 },

                // row 3
                new RelativeStep() { Chunk = new ulong[] {2, 1, 0}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 1}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 2}, Offset = 18, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 2}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 3}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 4}, Offset = 18, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {2, 1, 0}, Offset = 23, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 1}, Offset = 21, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 2}, Offset = 21, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 2}, Offset = 23, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 3}, Offset = 21, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 1, 4}, Offset = 21, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {2, 2, 0}, Offset = 26, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 2, 1}, Offset = 24, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 2, 2}, Offset = 24, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 2, 2}, Offset = 26, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 2, 3}, Offset = 24, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 2, 4}, Offset = 24, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {2, 3, 0}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 3, 1}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 3, 2}, Offset = 18, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 3, 2}, Offset = 20, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {2, 3, 3}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {2, 3, 4}, Offset = 18, Length = 1 },

                // row 4
                new RelativeStep() { Chunk = new ulong[] {3, 1, 0}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 1}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 0, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 3}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 4}, Offset = 0, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {3, 1, 0}, Offset = 5, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 1}, Offset = 3, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 3, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 5, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 3}, Offset = 3, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 4}, Offset = 3, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {3, 2, 0}, Offset = 8, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 1}, Offset = 6, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 2}, Offset = 6, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 2}, Offset = 8, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 3}, Offset = 6, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 4}, Offset = 6, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {3, 3, 0}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 1}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 2}, Offset = 0, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 2}, Offset = 2, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 3}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 4}, Offset = 0, Length = 1 },

                // row 5
                new RelativeStep() { Chunk = new ulong[] {3, 1, 0}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 1}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 9, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 3}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 4}, Offset = 9, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {3, 1, 0}, Offset = 14, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 1}, Offset = 12, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 12, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 2}, Offset = 14, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 3}, Offset = 12, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 1, 4}, Offset = 12, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {3, 2, 0}, Offset = 17, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 1}, Offset = 15, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 2}, Offset = 15, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 2}, Offset = 17, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 3}, Offset = 15, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 2, 4}, Offset = 15, Length = 1 },

                new RelativeStep() { Chunk = new ulong[] {3, 3, 0}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 1}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 2}, Offset = 9, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 2}, Offset = 11, Length = 1 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 3}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {3, 3, 4}, Offset = 9, Length = 1 }
            };

            // Act
            var actual = SelectionUtils
                .Walk(rank: 3, dims, chunkDims, selection)
                .ToArray();

            // Assert
            for (int i = 0; i < actual.Length; i++)
            {
                var actual_current = actual[i];
                var expected_current = expected[i];

                Assert.Equal(actual_current.Chunk[0], expected_current.Chunk[0]);
                Assert.Equal(actual_current.Chunk[1], expected_current.Chunk[1]);
                Assert.Equal(actual_current.Chunk[2], expected_current.Chunk[2]);
                Assert.Equal(actual_current.Offset, expected_current.Offset);
                Assert.Equal(actual_current.Length, expected_current.Length);
            }
        }

        [Theory]
        [InlineData(new ulong[] { 1, 3 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 })]
        [InlineData(new ulong[] { 1, 2, 3 }, new ulong[] { 1, 3 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 })]
        [InlineData(new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 3 }, new ulong[] { 1, 2, 3 })]
        [InlineData(new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 3 })]
        public void HyperslabSelectionThrowsForInvalidRank(ulong[] start, ulong[] stride, ulong[] count, ulong[] block)
        {
            // Arrange

            // Act
            void action() => _ = new HyperslabSelection(rank: 3, start, stride, count, block);

            // Assert
            Assert.Throws<RankException>(action);
        }

        [Theory]
        [InlineData(new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 0 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 })]
        [InlineData(new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 2 }, new ulong[] { 1, 2, 3 }, new ulong[] { 1, 2, 3 })]
        public void HyperslabSelectionThrowsForInvalidStride(ulong[] start, ulong[] stride, ulong[] count, ulong[] block)
        {
            // Arrange

            // Act
            void action() => _ = new HyperslabSelection(rank: 3, start, stride, count, block);

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void RegularHyperslabWalkThrowsForInvalidLimitsRank()
        {
            // Arrange
            var selection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 1, 25 },
                strides: new ulong[] { 4, 4 },
                counts: new ulong[] { 4, 4 },
                blocks: new ulong[] { 2, 3 });

            // Act
            void action() => _ = selection.Walk(limits: new[] { 100UL, 100UL, 100UL }).ToList();

            // Assert
            Assert.Throws<RankException>(action);
        }

        [Theory]
        [InlineData(new ulong[] { 14, 40 })]
        [InlineData(new ulong[] { 15, 39 })]
        public void RegularHyperslabWalkThrowsForExceedingLimits(ulong[] limits)
        {
            // Arrange
            var selection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 1, 25 },
                strides: new ulong[] { 4, 4 },
                counts: new ulong[] { 4, 4 },
                blocks: new ulong[] { 2, 3 });

            // Act
            void action() => _ = selection.Walk(limits: limits).ToList();

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        /*    /                                         /
         *   /  Dataset dimensions:  [14, 14, 14]      / 14
         *  /     Chunk dimensions:  [3, 3, 3]        /
         * /_  _  _  _  _  _  _  _  _  _  _  _  _  _ /
         * |x  x/ /| /      |        |        |     |
         * |x  x   |        |        |        |     |
         * |x  x  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     |
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     | 14
         * |       |        |        |        |     |
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|
         * |       |        |        |        |     |
         * |       |        |        |        |     |   /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|  /
         * |       |        |        |        |     | /
         * |_  _  _| _  _  _| _  _  _| _  _  _| _  _|/
         *                    14
         */
        public void CanWalk_IrregularHyperslab()
        {
            // Arrange
            var dims = new ulong[] { 14, 14, 14 };
            var chunkDims = new ulong[] { 3, 3, 3 };

            var selection = new IrregularHyperslabSelection(
                rank: 3,
                blockOffsets: new ulong[]
                {
                 /* starts..|...ends */
                    0, 0, 0, 0, 0, 2, // block 1
                    0, 1, 0, 0, 1, 4, // block 2
                    0, 2, 1, 0, 3, 4, // block 3
                    1, 0, 0, 2, 1, 2  // block 4
                }
            );

            var expected = new RelativeStep[]
            {
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 0, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 3, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 1}, Offset = 3, Length = 2 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 7, Length = 2 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 1}, Offset = 6, Length = 2 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 0}, Offset = 1, Length = 2 },
                new RelativeStep() { Chunk = new ulong[] {0, 1, 1}, Offset = 0, Length = 2 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 9, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 12, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 18, Length = 3 },
                new RelativeStep() { Chunk = new ulong[] {0, 0, 0}, Offset = 21, Length = 3 }
            };

            // Act
            var actual = SelectionUtils
                .Walk(rank: 3, dims, chunkDims, selection)
                .ToArray();

            // Assert
            for (int i = 0; i < actual.Length; i++)
            {
                var actual_current = actual[i];
                var expected_current = expected[i];

                Assert.Equal(actual_current.Chunk[0], expected_current.Chunk[0]);
                Assert.Equal(actual_current.Chunk[1], expected_current.Chunk[1]);
                Assert.Equal(actual_current.Chunk[2], expected_current.Chunk[2]);
                Assert.Equal(actual_current.Offset, expected_current.Offset);
                Assert.Equal(actual_current.Length, expected_current.Length);
            }
        }

        [Theory]
        [InlineData(new ulong[] { 16, 30 }, new ulong[] { 16, 25, 30 })]
        [InlineData(new ulong[] { 16, 25, 30 }, new ulong[] { 3, 3 })]
        public void WalkThrowsForInvalidRank(ulong[] dims, ulong[] chunkDims)
        {
            // Arrange
            var selection = new HyperslabSelection(1, 4, 3, 3);

            // Act
            void action() => _ = SelectionUtils.Walk(rank: 3, dims, chunkDims, selection).ToArray();

            // Assert
            Assert.Throws<RankException>(action);
        }

        [Theory]
        [InlineData(
            new ulong[] { 1, 1 }, new ulong[] { 4, 4 }, new ulong[] { 4, 4 }, new ulong[] { 2, 3 },
            new ulong[] { 1, 1 }, new ulong[] { 3, 3 }, new ulong[] { 5, 5 }, new ulong[] { 2, 3 })]
        [InlineData(
            new ulong[] { 1, 1 }, new ulong[] { 4, 4 }, new ulong[] { 4, 4 }, new ulong[] { 3, 4 },
            new ulong[] { 1, 1 }, new ulong[] { 4, 4 }, new ulong[] { 5, 5 }, new ulong[] { 3, 2 })]
        public void CopyThrowsForMismatchingSelectionSizes(
            ulong[] sourceStarts, ulong[] sourceStrides, ulong[] sourceCounts, ulong[] sourceBlocks,
            ulong[] targetStarts, ulong[] targetStrides, ulong[] targetCounts, ulong[] targetBlocks)
        {
            // Arrange
            var sourceSelection = new HyperslabSelection(rank: 2, sourceStarts, sourceStrides, sourceCounts, sourceBlocks);
            var targetSelection = new HyperslabSelection(rank: 2, targetStarts, targetStrides, targetCounts, targetBlocks);

            var decodeInfo = new DecodeInfo<int>(
                default!,
                default!,
                default!,
                default!,
                sourceSelection,
                targetSelection,
                default!,
                default!,
                default!,
                0,
                1
            );

            // Act
            void action() => SelectionUtils
                .DecodeAsync(default(SyncReader), sourceRank: 2, targetRank: 2, decodeInfo)
                .GetAwaiter()
                .GetResult();

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Theory]
        [InlineData(new ulong[] { 0, 2 }, new ulong[] { 3, 2 })]
        [InlineData(new ulong[] { 2, 2 }, new ulong[] { 0, 2 })]
        public async Task CanCopySmall2D_Count0_Or_Block0(ulong[] counts, ulong[] blocks)
        {
            // Arrange
            var datasetDims = new ulong[] { 10, 10 };
            var chunkDims = new ulong[] { 6, 6 };
            var memoryDims = new ulong[] { 10, 10 };

            var datasetSelection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 0, 1 },
                strides: new ulong[] { 5, 5 },
                counts: counts,
                blocks: blocks
            );

            var memorySelection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 1, 1 },
                strides: new ulong[] { 5, 5 },
                counts: counts,
                blocks: blocks
            );

            var decodeInfo = new DecodeInfo<int>(
                datasetDims,
                chunkDims,
                memoryDims,
                memoryDims,
                datasetSelection,
                memorySelection,
                indices => default!,
                indices => default,
                Decoder: default!,
                SourceTypeSize: 4,
                TargetTypeFactor: 1
            );

            // Act
            await SelectionUtils.DecodeAsync(default(AsyncReader), sourceRank: 2, targetRank: 2, decodeInfo);

            // Assert
        }

        [Theory]
        [InlineData(new ulong[] { 10, 0 }, new ulong[] { 10, 10 })]
        [InlineData(new ulong[] { 10, 10 }, new ulong[] { 10, 0 })]
        public async Task CanCopySmall2D_Dims0(ulong[] datasetDims, ulong[] memoryDims)
        {
            // Arrange
            var chunkDims = new ulong[] { 6, 6 };

            var datasetSelection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 0, 1 },
                strides: new ulong[] { 5, 5 },
                counts: new ulong[] { 2, 0 },
                blocks: new ulong[] { 3, 2 }
            );

            var memorySelection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 1, 1 },
                strides: new ulong[] { 5, 5 },
                counts: new ulong[] { 2, 0 },
                blocks: new ulong[] { 3, 2 }
            );

            var decodeInfo = new DecodeInfo<int>(
                datasetDims,
                chunkDims,
                memoryDims,
                memoryDims,
                datasetSelection,
                memorySelection,
                indices => default!,
                indices => default,
                Decoder: default!,
                SourceTypeSize: 4,
                TargetTypeFactor: 1
            );

            // Act
            await SelectionUtils.DecodeAsync(default(AsyncReader), sourceRank: 2, targetRank: 2, decodeInfo);

            // Assert        
        }

        [Fact]
        /* Source:
         *    /                             /
         *   /  Dataset dimensions:  [10, 10]
         *  /     Chunk dimensions:  [6, 6]
         * /_  _  _  _  _  _  _  _  _  _ /
         * |   X  X         | X  X      |
         * |   X  X         | X  X      |
         * |   X  X         | X  X      |
         * |                |           |
         * |                |           | 10
         * |_  X  X  _  _  _| X  X  _  _| 
         * |   X  X         | X  X      |
         * |   X  X         | X  X      |
         * |                |           | /
         * |_  _  _  _  _  _| _  _  _  _|/
         *               10
         */

        /* Target:
         *    /                             /
         *   /  Dataset dimensions:  [10, 10]
         *  /     Chunk dimensions:  [10, 10]
         * /_  _  _  _  _  _  _  _  _  _ /
         * |                            |
         * |   X  X  X  X  X  X         |
         * |   X  X  X  X  X  X         |
         * |                            |
         * |                            | 10
         * |   X  X  X  X  X  X         | 
         * |   X  X  X  X  X  X         |
         * |                            |
         * |                            | /
         * |_  _  _  _  _  _  _  _  _  _|/
         *               10
         */
        public void CanCopySmall2D_BlockGreater1_StrideGreater1()
        {
            // Arrange
            var datasetDims = new ulong[] { 10, 10 };
            var chunkDims = new ulong[] { 6, 6 };
            var memoryDims = new ulong[] { 10, 10 };

            var datasetSelection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 0, 1 },
                strides: new ulong[] { 5, 5 },
                counts: new ulong[] { 2, 2 },
                blocks: new ulong[] { 3, 2 }
            );

            var memorySelection = new HyperslabSelection(
                rank: 2,
                starts: new ulong[] { 1, 1 },
                strides: new ulong[] { 4, 25 /* should work */ },
                counts: new ulong[] { 2, 1 },
                blocks: new ulong[] { 2, 6 }
            );

            // s0
            var sourceBuffer0 = new byte[6 * 6 * sizeof(int)];
            var s0 = MemoryMarshal.Cast<byte, int>(sourceBuffer0);
            s0[1] = 1; s0[2] = 2; s0[7] = 5; s0[8] = 6; s0[13] = 9; s0[14] = 10; s0[31] = 13; s0[32] = 14;

            var sourceBuffer1 = new byte[6 * 6 * sizeof(int)];
            var s1 = MemoryMarshal.Cast<byte, int>(sourceBuffer1);
            s1[0] = 3; s1[1] = 4; s1[6] = 7; s1[7] = 8; s1[12] = 11; s1[13] = 12; s1[30] = 15; s1[31] = 16;

            var sourceBuffer2 = new byte[6 * 6 * sizeof(int)];
            var s2 = MemoryMarshal.Cast<byte, int>(sourceBuffer2);
            s2[1] = 17; s2[2] = 18; s2[7] = 21; s2[8] = 22;

            var sourceBuffer3 = new byte[6 * 6 * sizeof(int)];
            var s3 = MemoryMarshal.Cast<byte, int>(sourceBuffer3);
            s3[0] = 19; s3[1] = 20; s3[6] = 23; s3[7] = 24;

            var chunksBuffers = new Memory<byte>[]
            {
                sourceBuffer0,
                sourceBuffer1,
                sourceBuffer2,
                sourceBuffer3
            };

            var expected = new int[10 * 10];

            expected[11] = 1; expected[12] = 2; expected[13] = 3; expected[14] = 4; expected[15] = 5; expected[16] = 6;
            expected[21] = 7; expected[22] = 8; expected[23] = 9; expected[24] = 10; expected[25] = 11; expected[26] = 12;
            expected[51] = 13; expected[52] = 14; expected[53] = 15; expected[54] = 16; expected[55] = 17; expected[56] = 18;
            expected[61] = 19; expected[62] = 20; expected[63] = 21; expected[64] = 22; expected[65] = 23; expected[66] = 24;

            var actual = new int[10 * 10];
            var scaledDatasetDims = datasetDims.Select((dim, i) => MathUtils.CeilDiv(dim, chunkDims[i])).ToArray();

            var decodeInfo = new DecodeInfo<int>(
                datasetDims,
                chunkDims,
                memoryDims,
                memoryDims,
                datasetSelection,
                memorySelection,
                indices => Task.FromResult((IH5ReadStream)new SystemMemoryStream(chunksBuffers[indices.AsSpan().ToLinearIndex(scaledDatasetDims)])),
                indices => actual,
                Converter,
                SourceTypeSize: 4,
                TargetTypeFactor: 1
            );

            // Act
            SelectionUtils
                .DecodeAsync(default(SyncReader), sourceRank: 2, targetRank: 2, decodeInfo)
                .GetAwaiter()
                .GetResult();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
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
        public void CanCopySmall3D_Block1_Stride1()
        {
            // Arrange
            var datasetDims = new ulong[] { 2, 3, 6 };
            var chunkDims = new ulong[] { 1, 2, 3 };
            var memoryDims = new ulong[] { 5, 6, 11 };

            var sourceSelection = new HyperslabSelection(rank: 3,
                starts: new ulong[] { 0, 1, 1 },
                strides: new ulong[] { 1, 1, 1 },
                counts: new ulong[] { 1, 2, 3 },
                blocks: new ulong[] { 1, 1, 1 });

            var targetSelection = new HyperslabSelection(rank: 3,
                starts: new ulong[] { 1, 1, 1 },
                strides: new ulong[] { 1, 1, 1 },
                counts: new ulong[] { 3, 1, 2 },
                blocks: new ulong[] { 1, 1, 1 });

            // source
            var sourceBuffer1 = new byte[1 * 2 * 3 * sizeof(int)];
            var s1 = MemoryMarshal.Cast<byte, int>(sourceBuffer1);
            s1[0] = 0; s1[1] = 1; s1[2] = 2; s1[3] = 6; s1[4] = 7; s1[5] = 8;

            var sourceBuffer2 = new byte[1 * 2 * 3 * sizeof(int)];
            var s2 = MemoryMarshal.Cast<byte, int>(sourceBuffer2);
            s2[0] = 3; s2[1] = 4; s2[2] = 5; s2[3] = 9; s2[4] = 10; s2[5] = 11;

            var sourceBuffer3 = new byte[1 * 2 * 3 * sizeof(int)];
            var s3 = MemoryMarshal.Cast<byte, int>(sourceBuffer3);
            s3[0] = 12; s3[1] = 13; s3[2] = 14; s3[3] = 0; s3[4] = 0; s3[5] = 0;

            var sourceBuffer4 = new byte[1 * 2 * 3 * sizeof(int)];
            var s4 = MemoryMarshal.Cast<byte, int>(sourceBuffer4);
            s4[0] = 15; s4[1] = 16; s4[2] = 17; s4[3] = 0; s4[4] = 0; s4[5] = 0;

            var sourceBuffer5 = new byte[1 * 2 * 3 * sizeof(int)];
            var s5 = MemoryMarshal.Cast<byte, int>(sourceBuffer5);
            s5[0] = 18; s5[1] = 19; s5[2] = 20; s5[3] = 24; s5[4] = 25; s5[5] = 26;

            var sourceBuffer6 = new byte[1 * 2 * 3 * sizeof(int)];
            var s6 = MemoryMarshal.Cast<byte, int>(sourceBuffer6);
            s6[0] = 21; s6[1] = 22; s6[2] = 23; s6[3] = 27; s6[4] = 28; s6[5] = 29;

            var sourceBuffer7 = new byte[1 * 2 * 3 * sizeof(int)];
            var s7 = MemoryMarshal.Cast<byte, int>(sourceBuffer7);
            s7[0] = 30; s7[1] = 31; s7[2] = 32; s7[3] = 0; s7[4] = 0; s7[5] = 0;

            var sourceBuffer8 = new byte[1 * 2 * 3 * sizeof(int)];
            var s8 = MemoryMarshal.Cast<byte, int>(sourceBuffer8);
            s8[0] = 33; s8[1] = 34; s8[2] = 35; s8[3] = 0; s8[4] = 0; s8[5] = 0;

            var chunksBuffers = new Memory<byte>[]
            {
                sourceBuffer1,
                sourceBuffer2,
                sourceBuffer3,
                sourceBuffer4,
                sourceBuffer5,
                sourceBuffer6,
                sourceBuffer7,
                sourceBuffer8,
            };

            var expected = new int[5 * 6 * 11];

            expected[78] = 7;
            expected[79] = 8;
            expected[144] = 9;
            expected[145] = 13;
            expected[210] = 14;
            expected[211] = 15;

            var actual = new int[5 * 6 * 11];
            var scaledDatasetDims = datasetDims.Select((dim, i) => MathUtils.CeilDiv(dim, chunkDims[i])).ToArray();

            IH5ReadStream getSourceStreamAsync(ulong[] indices) 
                => new SystemMemoryStream(chunksBuffers[indices.AsSpan().ToLinearIndex(scaledDatasetDims)]);

            var decodeInfo = new DecodeInfo<int>(
                datasetDims,
                chunkDims,
                memoryDims,
                memoryDims,
                sourceSelection,
                targetSelection,
                indices => Task.FromResult(getSourceStreamAsync(indices)),
                indices => actual,
                Converter,
                SourceTypeSize: 4,
                TargetTypeFactor: 1
            );

            // Act
            SelectionUtils
                .DecodeAsync(default(SyncReader), sourceRank: 3, targetRank: 3, decodeInfo)
                .GetAwaiter()
                .GetResult();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        /*    /                             /
         *   /  Dataset dimensions:  [10, 10, 10]
         *  /     Chunk dimensions:  [6, 6, 3]
         * /_  _  _  _  _  _  _  _  _  _ /
         * |                |           |
         * |                |           |
         * |                |           |
         * |                |           |
         * |            X   | X     X   | 10
         * |_  _  _  _  _  _| _  _  _  _| 
         * |            X   | X     X   |
         * |                |           |
         * |                |           | /
         * |_  _  _  _  _  _| _  _  _  _|/
         *               10
         */
        public void CanCopySmall3D_Block1_StrideGreater1()
        {
            // Arrange
            var datasetDims = new ulong[] { 10, 10, 10 };
            var chunkDims = new ulong[] { 6, 6, 3 };
            var memoryDims = new ulong[] { 11, 11, 12 };

            var datasetSelection = new HyperslabSelection(
                rank: 3,
                starts: new ulong[] { 4, 4, 2 },
                strides: new ulong[] { 2, 2, 4 },
                counts: new ulong[] { 2, 3, 2 },
                blocks: new ulong[] { 1, 1, 1 }
            );

            var memorySelection = new HyperslabSelection(
                rank: 3,
                starts: new ulong[] { 2, 1, 1 },
                strides: new ulong[] { 3, 3, 3 },
                counts: new ulong[] { 2, 2, 3 },
                blocks: new ulong[] { 1, 1, 1 }
            );

            // s0
            var sourceBuffer0 = new byte[6 * 6 * 3 * sizeof(int)];
            var s0 = MemoryMarshal.Cast<byte, int>(sourceBuffer0);
            s0[86] = 1;

            // s2
            var sourceBuffer2 = new byte[6 * 6 * 3 * sizeof(int)];
            var s2 = MemoryMarshal.Cast<byte, int>(sourceBuffer2);
            s2[84] = 2;

            // s4
            var sourceBuffer4 = new byte[6 * 6 * 3 * sizeof(int)];
            var s4 = MemoryMarshal.Cast<byte, int>(sourceBuffer4);
            s4[74] = 3;
            s4[80] = 5;

            // s6
            var sourceBuffer6 = new byte[6 * 6 * 3 * sizeof(int)];
            var s6 = MemoryMarshal.Cast<byte, int>(sourceBuffer6);
            s6[72] = 4;
            s6[78] = 6;

            // s8
            var sourceBuffer8 = new byte[6 * 6 * 3 * sizeof(int)];
            var s8 = MemoryMarshal.Cast<byte, int>(sourceBuffer8);
            s8[14] = 7;

            // s10
            var sourceBuffer10 = new byte[6 * 6 * 3 * sizeof(int)];
            var s10 = MemoryMarshal.Cast<byte, int>(sourceBuffer10);
            s10[12] = 8;

            // s12
            var sourceBuffer12 = new byte[6 * 6 * 3 * sizeof(int)];
            var s12 = MemoryMarshal.Cast<byte, int>(sourceBuffer12);
            s12[2] = 9;
            s12[8] = 11;

            // s14
            var sourceBuffer14 = new byte[6 * 6 * 3 * sizeof(int)];
            var s14 = MemoryMarshal.Cast<byte, int>(sourceBuffer14);
            s14[0] = 10;
            s14[6] = 12;

            var chunksBuffers = new Memory<byte>[]
            {
                sourceBuffer0, default, sourceBuffer2, default, sourceBuffer4, default, sourceBuffer6, default,
                sourceBuffer8, default, sourceBuffer10, default, sourceBuffer12, default, sourceBuffer14, default
            };

            var expected = new int[11 * 11 * 12];
            var scaledDatasetDims = datasetDims.Select((dim, i) => MathUtils.CeilDiv(dim, chunkDims[i])).ToArray();

            expected[277] = 1;
            expected[280] = 2;
            expected[283] = 3;
            expected[313] = 4;
            expected[316] = 5;
            expected[319] = 6;
            expected[673] = 7;
            expected[676] = 8;
            expected[679] = 9;
            expected[709] = 10;
            expected[712] = 11;
            expected[715] = 12;

            var actual = new int[11 * 11 * 12];

            IH5ReadStream getSourceStreamAsync(ulong[] indices) 
                => new SystemMemoryStream(chunksBuffers[indices.AsSpan().ToLinearIndex(scaledDatasetDims)]);

            var decodeInfo = new DecodeInfo<int>(
                datasetDims,
                chunkDims,
                memoryDims,
                memoryDims,
                datasetSelection,
                memorySelection,
                indices => Task.FromResult(getSourceStreamAsync(indices)),
                indices => actual,
                Converter,
                SourceTypeSize: 4,
                TargetTypeFactor: 1
            );

            // Act
            SelectionUtils
                .DecodeAsync(default(SyncReader), sourceRank: 3, targetRank: 3, decodeInfo)
                .GetAwaiter()
                .GetResult();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact(Skip = "C library does not give correct 'expected' array :-/")]
        public void CanCopyLikeCLib()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var datasetDims = new ulong[] { 25, 25, 4 };
                var chunkDims = new ulong[] { 7, 20, 3 };
                var memoryDims = new ulong[] { 75, 25 };

                var datasetSelection = new HyperslabSelection(
                    rank: 3,
                    starts: new ulong[] { 2, 2, 0 },
                    strides: new ulong[] { 5, 8, 2 },
                    counts: new ulong[] { 5, 3, 2 },
                    blocks: new ulong[] { 3, 5, 2 }
                );

                var memorySelection = new HyperslabSelection(
                    rank: 2,
                    starts: new ulong[] { 2, 1 },
                    strides: new ulong[] { 35, 17 },
                    counts: new ulong[] { 2, 1 },
                    blocks: new ulong[] { 30, 15 }
                );

                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDatasetForHyperslab(fileId));
                var expected = new int[memoryDims[0] * memoryDims[1]];

                {
                    var fileId = H5F.open(filePath, H5F.ACC_RDONLY);
                    var datasetId = H5D.open(fileId, "/chunked/hyperslab");

                    var datasetSpaceId = H5S.create_simple(rank: 3, datasetDims, datasetDims);
                    var res1 = H5S.select_hyperslab(datasetSpaceId, H5S.seloper_t.SET,
                        datasetSelection.Starts.ToArray(), datasetSelection.Strides.ToArray(),
                        datasetSelection.Counts.ToArray(), datasetSelection.Blocks.ToArray());

                    var memorySpaceId = H5S.create_simple(rank: 2, memoryDims, memoryDims);
                    var res2 = H5S.select_hyperslab(memorySpaceId, H5S.seloper_t.SET,
                        memorySelection.Starts.ToArray(), memorySelection.Strides.ToArray(),
                        memorySelection.Counts.ToArray(), memorySelection.Blocks.ToArray());

                    unsafe
                    {
                        fixed (int* ptr = expected)
                        {
                            var res3 = H5D.read(datasetId, H5T.NATIVE_INT32, memorySpaceId, datasetSpaceId, 0, new IntPtr(ptr));

                            if (res3 < 0)
                                throw new Exception("Unable to read data.");
                        }
                    }

                    _ = H5S.close(memorySpaceId);
                    _ = H5S.close(datasetSpaceId);
                    _ = H5D.close(datasetId);
                    _ = H5F.close(fileId);
                }

                using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
                var dataset = (NativeDataset)root.Dataset("/chunked/hyperslab");

                var datasetInfo = new DatasetInfo(
                    Space: dataset.InternalDataspace,
                    Type: dataset.InternalDataType,
                    Layout: dataset.InternalDataLayout,
                    FillValue: dataset.InternalFillValue,
                    FilterPipeline: dataset.InternalFilterPipeline,
                    ExternalFileList: dataset.InternalExternalFileList
                );

                var context = new NativeContext(default!, default!);

                /* get intermediate data (only for Matlab visualization) */
                var intermediate = new int[datasetDims[0] * datasetDims[1] * datasetDims[2]];

                var h5dIntermediate = H5D_Chunk.Create(context, datasetInfo, default);
                h5dIntermediate.Initialize();

                var decodeInfoInterMediate = new DecodeInfo<int>(
                    datasetDims,
                    chunkDims,
                    datasetDims,
                    datasetDims,
                    datasetSelection,
                    datasetSelection,
                    indices => h5dIntermediate.GetReadStreamAsync(default(SyncReader), indices),
                    indices => intermediate,
                    Converter,
                    SourceTypeSize: 4,
                    TargetTypeFactor: 1
                );

                SelectionUtils
                    .DecodeAsync(default(SyncReader), sourceRank: 3, targetRank: 3, decodeInfoInterMediate)
                    .GetAwaiter()
                    .GetResult();

                /* get actual data */
                var actual = new int[memoryDims[0] * memoryDims[1]];

                var h5d = H5D_Chunk.Create(context, datasetInfo, default);
                h5d.Initialize();

                var decodeInfo = new DecodeInfo<int>(
                    datasetDims,
                    chunkDims,
                    memoryDims,
                    memoryDims,
                    datasetSelection,
                    memorySelection,
                    indices => h5d.GetReadStreamAsync(default(SyncReader), indices),
                    indices => actual,
                    Decoder: default!,
                    SourceTypeSize: 4,
                    TargetTypeFactor: 1
                );

                // Act
                SelectionUtils
                    .DecodeAsync(default(SyncReader), sourceRank: 3, targetRank: 2, decodeInfo)
                    .GetAwaiter()
                    .GetResult();

                //var intermediateForMatlab = string.Join(',', intermediate.ToArray().Select(value => value.ToString()));
                //var actualForMatlab = string.Join(',', actual.ToArray().Select(value => value.ToString()));
                //var expectedForMatlab = string.Join(',', expected.ToArray().Select(value => value.ToString()));

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }
    }
}