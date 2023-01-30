namespace PureHDF
{
    internal class HyperslabSelectionInfo1 : HyperslabSelectionInfo
    {
        #region Constructors

        public HyperslabSelectionInfo1(H5BaseReader reader)
        {
            // reserved
            reader.ReadBytes(4);

            // length
            _ = reader.ReadUInt32();

            // rank
            Rank = reader.ReadUInt32();

            // block count
            BlockCount = reader.ReadUInt32();

            // block offsets / compact coordinates / compact dimensions
            CompactDimensions = new ulong[Rank];

            var totalOffsetGroups = BlockCount * Rank;
            BlockOffsets = new uint[totalOffsetGroups * 2];
            CompactBlockCoordinates = new uint[totalOffsetGroups];

            Initialize(reader, BlockOffsets, CompactBlockCoordinates, CompactDimensions);
        }

        #endregion

        #region Properties

        public uint BlockCount { get; set; }
        public uint[] BlockOffsets { get; set; }
        public uint[] CompactBlockCoordinates { get; set; }

        #endregion

        #region Methods

        private void Initialize(H5BaseReader reader, uint[] blockOffsets, uint[] compactBlockCoordinates, ulong[] compactDimensions)
        {
            var isFirstBlock = true;
            Span<uint> previousStarts = stackalloc uint[(int)Rank];
            Span<uint> previousEnds = stackalloc uint[(int)Rank];

            // for each block
            for (int block = 0; block < BlockCount; block++)
            {
                var blockOffsetsIndex = block * Rank;

                // for each dimension
                for (int dimension = 0; dimension < Rank; dimension++)
                {
                    var start = reader.ReadUInt32();
                    var end = reader.ReadUInt32();
                    var dimensionIndex = blockOffsetsIndex + dimension;

                    // store block offsets
                    blockOffsets[dimensionIndex * 2 + 0] = start;
                    blockOffsets[dimensionIndex * 2 + 1] = end;

                    // compute compact block coordinates
                    if (!isFirstBlock)
                    {
                        // the offset of the current dimension changed
                        if (start > previousStarts[dimension])
                            compactDimensions[dimension] += previousEnds[dimension] - previousStarts[dimension] + 1;
                    }

                    compactBlockCoordinates[dimensionIndex] = (uint)compactDimensions[dimension];

                    previousStarts[dimension] = start;
                    previousEnds[dimension] = end;
                }

                isFirstBlock = false;
            }

            // calculate compact dimensions
            for (int dimension = 0; dimension < Rank; dimension++)
            {
                compactDimensions[dimension] += previousEnds[dimension] - previousStarts[dimension] + 1;
            }
        }

        #endregion
    }
}
