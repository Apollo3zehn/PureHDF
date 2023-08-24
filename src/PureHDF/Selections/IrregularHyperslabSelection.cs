namespace PureHDF.Selections;

/// <summary>
/// An irregular hyperslab is a selection of elements from a hyper rectangle.
/// </summary>
public class IrregularHyperslabSelection : Selection
{
    private readonly ulong[] _blockOffsets;

    private readonly int _blockCount;

    internal IrregularHyperslabSelection(int rank, ulong[] blockOffsets)
    {
        Rank = rank;
        _blockOffsets = blockOffsets;
        _blockCount = blockOffsets.Length / rank / 2;

        // calculate total element count
        var totalElementCount = 0UL;

        for (int blockIndex = 0; blockIndex < _blockCount; blockIndex++)
        {
            var elementCount = 1UL;
            var blockOffsetsIndex = blockIndex * rank * 2;

            for (int dimension = 0; dimension < rank; dimension++)
            {
                var start = _blockOffsets[blockOffsetsIndex + 0 + dimension];
                var end = _blockOffsets[blockOffsetsIndex + rank + dimension] + 1;

                elementCount *= end - start;
            }

            totalElementCount += elementCount;
        }

        TotalElementCount = totalElementCount;
    }

    /// <summary>
    /// Gets the rank of the selection.
    /// </summary>
    public int Rank { get; }

    /// <inheritdoc />
    public override ulong TotalElementCount { get; }

    /// <inheritdoc />
    public override IEnumerable<Step> Walk(ulong[] limits)
    {
        /* the following algorithm is a simple version of that of the HyperslabSelection */

        /* prepare some useful arrays */
        var lastDim = Rank - 1;
        var offsets = new ulong[Rank];
        var starts = new ulong[Rank];
        var stops = new ulong[Rank];
        var coordinates = new ulong[Rank];

        for (uint blockIndex = 0; blockIndex < _blockCount; blockIndex++)
        {
            var blockOffsetsIndex = blockIndex * Rank * 2;

            for (var dimension = 0; dimension < Rank; dimension++)
            {
                starts[dimension] = _blockOffsets[blockOffsetsIndex + 0 + dimension];
                stops[dimension] = _blockOffsets[blockOffsetsIndex + Rank + dimension] + 1;

                offsets[dimension] = starts[dimension];
            }

            /* prepare the step */
            var step = new Step() { Coordinates = coordinates };  

            /* loop until all data have been processed */
            while (true)
            {
                var done = false;

                /* compute number of consecutive points in current slice */
                var totalLength = stops[^1] - starts[^1];

                /* return next step */
                offsets
                    .AsSpan()
                    .CopyTo(step.Coordinates);

                step.ElementCount = totalLength;

                yield return step;

                /* update offsets array */
                offsets[lastDim] += totalLength;

                /* iterate backwards through all dimensions */
                for (int dimension = lastDim; dimension >= 0; dimension--)
                {
                    if (dimension != lastDim)
                    {
                        /* go one step forward */
                        offsets[dimension] += 1;
                    }

                    /* if the current slice is fully processed */
                    if (offsets[dimension] >= stops[dimension])
                    {
                        /* if there is more to process, reset the offset and 
                        * repeat the loop for the next higher dimension */
                        if (dimension > 0)
                            offsets[dimension] = starts[dimension];

                        /* else, we are done! */
                        else
                            done = true;
                    }

                    /* otherwise, break the loop */
                    else
                    {
                        break;
                    }
                }

                if (done)
                    break;
            }
        }
    }
}