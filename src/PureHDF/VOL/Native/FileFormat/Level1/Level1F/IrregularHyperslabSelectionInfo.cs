namespace PureHDF.VOL.Native;

internal class IrregularHyperslabSelectionInfo : HyperslabSelectionInfo
{
    #region Constructors

    public IrregularHyperslabSelectionInfo(H5DriverBase driver, uint rank, byte encodeSize)
    {
        Rank = rank;

        // block count
        BlockCount = H5S_SEL.ReadEncodedValue(driver, encodeSize);

        // block offsets / compact starts
        var totalOffsetGroups = BlockCount * Rank;
        BlockOffsets = new ulong[totalOffsetGroups * 2];
        BlockLinearIndices = new ulong[BlockCount];

        Initialize(
            driver,
            encodeSize,
            BlockOffsets,
            BlockLinearIndices);
    }

    #endregion

    #region Properties

    public ulong BlockCount { get; set; }
    public ulong[] BlockOffsets { get; set; }
    public ulong[] BlockLinearIndices { get; set; }

    #endregion

    #region Methods

    private void Initialize(
        H5DriverBase driver,
        byte encodeSize,
        ulong[] blockOffsets,
        ulong[] blockLinearIndices)
    {
        var isFirst = true;

        Span<ulong> previousStarts = stackalloc ulong[(int)Rank];
        Span<ulong> previousEnds = stackalloc ulong[(int)Rank];
        Span<ulong> sizes = stackalloc ulong[(int)Rank];

        // for each block
        for (ulong block = 0; block < BlockCount; block++)
        {
            var blockOffsetsIndex = block * Rank;

            // block linear index
            if (isFirst)
            {
                blockLinearIndices[0] = 0;
            }

            else
            {
                for (int dimension = 0; dimension < Rank; dimension++)
                {
                    sizes[dimension] = previousEnds[dimension] - previousStarts[dimension] + 1;
                }

                var previousBlockElementCount = 1UL;

                for (int dimension = 0; dimension < Rank; dimension++)
                {
                    previousBlockElementCount *= sizes[dimension];
                }

                blockLinearIndices[block] = blockLinearIndices[block - 1] + previousBlockElementCount;
            }

            // starts
            for (int dimension = 0; dimension < Rank; dimension++)
            {
                var start = H5S_SEL.ReadEncodedValue(driver, encodeSize);
                var dimensionIndex = (int)blockOffsetsIndex * 2 + 0 + dimension;
                blockOffsets[dimensionIndex] = start;
                previousStarts[dimension] = start;
            }

            // ends
            for (int dimension = 0; dimension < Rank; dimension++)
            {
                var end = H5S_SEL.ReadEncodedValue(driver, encodeSize);
                var dimensionIndex = (int)blockOffsetsIndex * 2 + Rank + dimension;
                blockOffsets[dimensionIndex] = end;
                previousEnds[dimension] = end;
            }

            isFirst = false;
        }
    }

    #endregion
}