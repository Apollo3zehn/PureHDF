namespace PureHDF
{
    internal class IrregularHyperslabSelectionInfo : HyperslabSelectionInfo
    {
        #region Constructors

        public IrregularHyperslabSelectionInfo(H5BaseReader reader, uint rank, byte encodeSize)
        {
            Rank = rank;

            // block count
            BlockCount = ReadEncodedValue(reader, encodeSize);

            // block offsets / compact starts / compact dimensions
            CompactDimensions = new ulong[Rank];

            var totalOffsetGroups = BlockCount * Rank;
            BlockOffsets = new ulong[totalOffsetGroups * 2];
            CompactBlockStarts = new ulong[totalOffsetGroups];
            CompactBlockEnds = new ulong[totalOffsetGroups];

            Initialize(
                reader, 
                encodeSize, 
                BlockOffsets, 
                CompactBlockStarts, 
                CompactBlockEnds, 
                CompactDimensions);
        }

        #endregion

        #region Properties

        public ulong BlockCount { get; set; }
        public ulong[] BlockOffsets { get; set; }
        public ulong[] CompactBlockStarts { get; set; }
        public ulong[] CompactBlockEnds { get; set; }

        #endregion

        #region Methods

        private void Initialize(
            H5BaseReader reader,
            byte encodeSize, 
            ulong[] blockOffsets, 
            ulong[] compactBlockStarts, 
            ulong[] compactBlockEnds, 
            ulong[] compactDimensions)
        {
            var isFirstBlock = true;
            Span<ulong> previousStarts = stackalloc ulong[(int)Rank];
            Span<ulong> previousEnds = stackalloc ulong[(int)Rank];

            // for each block
            for (ulong block = 0; block < BlockCount; block++)
            {
                var blockOffsetsIndex = block * Rank;

                // for each dimension
                for (int dimension = 0; dimension < Rank; dimension++)
                {
                    // start
                    var start = ReadEncodedValue(reader, encodeSize);
                    var end = ReadEncodedValue(reader, encodeSize);
                    var dimensionIndex = (int)blockOffsetsIndex + dimension;

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

                    compactBlockStarts[dimensionIndex] = (uint)compactDimensions[dimension];
                    compactBlockEnds[dimensionIndex] = (uint)compactDimensions[dimension] + (end - start);

                    previousStarts[dimension] = start;
                    previousEnds[dimension] = end;
                }

                isFirstBlock = false;
            }

            // compute compact dimensions
            for (int dimension = 0; dimension < Rank; dimension++)
            {
                compactDimensions[dimension] += previousEnds[dimension] - previousStarts[dimension] + 1;
            }
        }

        #endregion
    }
}
