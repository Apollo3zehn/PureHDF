namespace PureHDF.VOL.Native;

internal record class IrregularHyperslabSelectionInfo(
    uint Rank,
    ulong BlockCount,
    ulong[] BlockOffsets,
    ulong[] BlockLinearIndices
) : HyperslabSelectionInfo(Rank: Rank)
{
    /* 
     * block offsets 
     *
     * block 0
     *   starts
     *     0
     *     1
     *     2
     *
     *   ends
     *     3
     *     4
     *     5
     *
     *  block 1
     *   starts
     *     6
     *     ...
     */

    public static IrregularHyperslabSelectionInfo Decode(H5DriverBase driver, uint rank, byte encodeSize)
    {
        // block count
        var blockCount = H5S_SEL.ReadEncodedValue(driver, encodeSize);

        // block offsets / compact starts
        var totalOffsetGroups = blockCount * rank;
        var blockOffsets = new ulong[totalOffsetGroups * 2];
        var blockLinearIndices = new ulong[blockCount];

        Initialize(
            driver,
            rank,
            encodeSize,
            blockCount,
            blockOffsets,
            blockLinearIndices);

        return new IrregularHyperslabSelectionInfo(
            Rank: rank,
            BlockCount: blockCount,
            BlockOffsets: blockOffsets,
            BlockLinearIndices: blockLinearIndices
        );
    }

    private static void Initialize(
        H5DriverBase driver,
        uint rank,
        byte encodeSize,
        ulong blockCount,
        ulong[] blockOffsets,
        ulong[] blockLinearIndices)
    {
        var isFirst = true;

        Span<ulong> previousStarts = stackalloc ulong[(int)rank];
        Span<ulong> previousEnds = stackalloc ulong[(int)rank];
        Span<ulong> sizes = stackalloc ulong[(int)rank];

        // for each block
        for (ulong block = 0; block < blockCount; block++)
        {
            var blockOffsetsIndex = block * rank;

            // block linear index
            if (isFirst)
            {
                blockLinearIndices[0] = 0;
            }

            else
            {
                for (int dimension = 0; dimension < rank; dimension++)
                {
                    sizes[dimension] = previousEnds[dimension] - previousStarts[dimension] + 1;
                }

                var previousBlockElementCount = 1UL;

                for (int dimension = 0; dimension < rank; dimension++)
                {
                    previousBlockElementCount *= sizes[dimension];
                }

                blockLinearIndices[block] = blockLinearIndices[block - 1] + previousBlockElementCount;
            }

            // starts
            for (int dimension = 0; dimension < rank; dimension++)
            {
                var start = H5S_SEL.ReadEncodedValue(driver, encodeSize);
                var dimensionIndex = (int)blockOffsetsIndex * 2 + 0 + dimension;
                blockOffsets[dimensionIndex] = start;
                previousStarts[dimension] = start;
            }

            // ends
            for (int dimension = 0; dimension < rank; dimension++)
            {
                var end = H5S_SEL.ReadEncodedValue(driver, encodeSize);
                var dimensionIndex = (int)blockOffsetsIndex * 2 + rank + dimension;
                blockOffsets[dimensionIndex] = end;
                previousEnds[dimension] = end;
            }

            isFirst = false;
        }
    }
}