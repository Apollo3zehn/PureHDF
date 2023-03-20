namespace PureHDF.VOL.Native;

internal class H5S_SEL_HYPER : H5S_SEL
{
    #region Fields

    private uint _version;

    #endregion

    #region Constructors

    public H5S_SEL_HYPER(H5DriverBase driver)
    {
        // version
        Version = driver.ReadUInt32();

        // SelectionInfo
        SelectionInfo = HyperslabSelectionInfo.Create(driver, Version);
    }

    #endregion

    #region Properties

    public uint Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (!(1 <= value && value <= 3))
                throw new FormatException($"Only version 1, version 2 and version 3 instances of type {nameof(H5S_SEL_HYPER)} are supported.");

            _version = value;
        }
    }

    public HyperslabSelectionInfo SelectionInfo { get; set; }

    public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
    {
        var success = false;

        ulong linearIndex = default;
        ulong maxCount = default;

        var rank = SelectionInfo.Rank;

        if (SelectionInfo is IrregularHyperslabSelectionInfo irregular)
        {
            Span<ulong> blockDimensions = stackalloc ulong[(int)rank];
            Span<ulong> blockCoordinates = stackalloc ulong[(int)rank];

            // find block which envelops the provided coordinates
            for (uint blockIndex = 0; blockIndex < irregular.BlockCount; blockIndex++)
            {
                success = true;
                var blockOffsetsIndex = blockIndex * rank * 2;

                // for each dimension
                for (var dimension = 0; dimension < rank; dimension++)
                {
                    var start = irregular.BlockOffsets[blockOffsetsIndex + 0 + (ulong)dimension];
                    var end = irregular.BlockOffsets[blockOffsetsIndex + rank + (ulong)dimension];
                    var coordinate = coordinates[dimension];

                    if (start <= coordinate && coordinate <= end)
                    {
                        blockCoordinates[dimension] = coordinate - start;
                        blockDimensions[dimension] = end - start + 1;
                    }
                    else
                    {
                        // Return max count if this is the fastest changing 
                        // dimension since there will be no better matching 
                        // blocks coming.
                        if (dimension == rank - 1 && start > coordinate)
                        {
                            // find distance until the next block begins, 
                            // otherwise 0 (i.e. there is no block)
                            maxCount = start - coordinate;

                            return new LinearIndexResult(
                                Success: false,
                                LinearIndex: default,
                                maxCount);
                        }

                        // Continue searching
                        else
                        {
                            success = false;
                            break;
                        }
                    }
                }

                if (success)
                {
                    var blockLinearStartIndex = irregular.BlockLinearIndices[blockIndex];
                    var blockLinearIndex = Utils.ToLinearIndex(blockCoordinates, blockDimensions);

                    linearIndex = blockLinearStartIndex + blockLinearIndex;
                    maxCount = blockDimensions[^1] - blockCoordinates[^1];
                    break;
                }
            }
        }

        else if (SelectionInfo is RegularHyperslabSelectionInfo regular)
        {
            success = true;
            Span<ulong> compactCoordinates = stackalloc ulong[(int)rank];

            // find hyperslab parameters which envelops the provided coordinates
            for (int dimension = 0; dimension < rank; dimension++)
            {
                var start = regular.Starts[dimension];
                var stride = regular.Strides[dimension];
                var count = regular.Counts[dimension];
                var block = regular.Blocks[dimension];
                var coordinate = coordinates[dimension];

                bool alreadyFailed = default;
                ulong actualCount = default;
                ulong blockOffset = default;

                if (coordinate < start)
                {
                    alreadyFailed = true;
                }
                else
                {
                    actualCount = (ulong)Math.DivRem((long)(coordinate - start), (long)stride, out var blockOffsetLong);
                    blockOffset = (ulong)blockOffsetLong;
                }

                if (alreadyFailed || actualCount >= count || blockOffset >= block)
                {
                    // Return max count if this is the fastest changing 
                    // dimension since there will be no better matching 
                    // blocks coming.
                    if (dimension == rank - 1 && start > coordinate)
                    {
                        // find distance until the next block begins, 
                        // otherwise 0 (i.e. there is no block)
                        maxCount = start - coordinate;

                        return new LinearIndexResult(
                            Success: false,
                            LinearIndex: default,
                            maxCount);
                    }

                    // Continue searching
                    else
                    {
                        success = false;
                        break;
                    }
                }

                compactCoordinates[dimension] = actualCount * block + blockOffset;
                linearIndex = Utils.ToLinearIndex(compactCoordinates, regular.CompactDimensions);
                maxCount = block - blockOffset;
            }
        }

        else
        {
            throw new NotSupportedException($"The hyperslab selection info of type {typeof(HyperslabSelectionInfo).Name} is not supported.");
        }

        // TODO theoretically the previous steps can fail if linear index is outside the dataset
        if (success)
        {
            return new LinearIndexResult(
                Success: true,
                LinearIndex: linearIndex,
                maxCount);
        }

        // nothing found
        else
            return default;
    }

    public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
    {
        var rank = SelectionInfo.Rank;
        var coordinates = new ulong[rank];
        var success = false;
        ulong maxCount = default;

        if (SelectionInfo is IrregularHyperslabSelectionInfo irregular)
        {
            Span<ulong> blockDimensions = stackalloc ulong[(int)rank];
            Span<ulong> blockStarts = stackalloc ulong[(int)rank];
            Span<ulong> blockCoordinates = stackalloc ulong[(int)rank];

            // find block which envelops the provided linear index
            for (ulong blockIndex = irregular.BlockCount - 1; blockIndex >= 0; blockIndex--)
            {
                var blockStartLinearIndex = irregular.BlockLinearIndices[blockIndex];

                // TODO binary search
                if (blockStartLinearIndex <= linearIndex)
                {
                    var blockLinearIndex = linearIndex - blockStartLinearIndex;
                    var blockOffsetsIndex = blockIndex * rank * 2;

                    // Compute block dimension and fill starts array
                    for (var dimension = 0; dimension < rank; dimension++)
                    {
                        var start = irregular.BlockOffsets[blockOffsetsIndex + 0 + (ulong)dimension];
                        var end = irregular.BlockOffsets[blockOffsetsIndex + rank + (ulong)dimension];

                        blockStarts[dimension] = start;
                        blockDimensions[dimension] = end - start + 1;
                    }

                    // Compute block coordinates
                    Utils.ToCoordinates(blockLinearIndex, blockDimensions, blockCoordinates);

                    // Compute absolute coordinates
                    for (var dimension = 0; dimension < rank; dimension++)
                    {
                        coordinates[dimension] = blockStarts[dimension] + blockCoordinates[dimension];
                    }

                    // Compute max count
                    maxCount = blockDimensions[^1] - blockCoordinates[^1];

                    // TODO theoretically the previous steps can fail if relative coordinates are outside the block
                    success = true;
                    break;
                }
            }
        }

        else if (SelectionInfo is RegularHyperslabSelectionInfo regular)
        {
            success = true;

            var compactCoordinates = Utils.ToCoordinates(linearIndex, regular.CompactDimensions);

            // find hyperslab parameters which envelops the provided linear index
            for (int dimension = 0; dimension < rank; dimension++)
            {
                var start = regular.Starts[dimension];
                var stride = regular.Strides[dimension];
                var count = regular.Counts[dimension];
                var block = regular.Blocks[dimension];
                var compactCoordinate = compactCoordinates[dimension];

                var actualCount = (ulong)Math.DivRem((long)compactCoordinate, (long)block, out var blockOffsetLong);
                var blockOffset = (ulong)blockOffsetLong;

                if (actualCount >= count || blockOffset >= block)
                {
                    success = false;
                    break;
                }

                coordinates[dimension] = start + actualCount * stride + blockOffset;
                maxCount = block - blockOffset;

                // TODO theoretically the previous steps can fail if relative coordinates are outside the block
            }
        }

        else
        {
            throw new NotSupportedException($"The hyperslab selection info of type {typeof(HyperslabSelectionInfo).Name} is not supported.");
        }

        if (success)
            return new CoordinatesResult(coordinates, maxCount);

        else
            throw new Exception("This should never happen.");
    }

    #endregion
}