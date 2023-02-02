namespace PureHDF
{
    internal class H5S_SEL_HYPER : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_HYPER(H5BaseReader reader)
        {
            // version
            Version = reader.ReadUInt32();

            // SelectionInfo
            SelectionInfo = HyperslabSelectionInfo.Create(reader, Version);
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
                if (!(1 <= value && value <= 2))
                    throw new FormatException($"Only version 1 and version 2 instances of type {nameof(H5S_SEL_HYPER)} are supported.");

                _version = value;
            }
        }

        public HyperslabSelectionInfo SelectionInfo { get; set; }

        public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
        {
            var success = false;

            ulong maxCount = default;
            Span<ulong> compactCoordinates = stackalloc ulong[(int)SelectionInfo.Rank];

            if (SelectionInfo is IrregularHyperslabSelectionInfo irregular)
            {
                // for each block
                for (uint blockIndex = 0; blockIndex < irregular.BlockCount; blockIndex++)
                {
                    success = true;
                    var offsetsGroupIndex = blockIndex * irregular.Rank;

                    // for each dimension
                    for (var dimension = 0; dimension < irregular.Rank; dimension++)
                    {
                        var dimensionIndex = offsetsGroupIndex + dimension;
                        var start = irregular.BlockOffsets[dimensionIndex * 2 + 0];
                        var end = irregular.BlockOffsets[dimensionIndex * 2 + 1];
                        var coordinate = coordinates[dimension];

                        if (start <= coordinate && coordinate <= end)
                        {
                            var compactStart = irregular.CompactBlockStarts[dimensionIndex];

                            compactCoordinates[dimension] = compactStart + (coordinate - start);
                            maxCount = end - coordinate + 1;
                        }
                        else
                        {
                            success = false;
                            break;
                        }
                    }

                    if (success)
                        break;
                }
            }

            else if (SelectionInfo is RegularHyperslabSelectionInfo regular)
            {
                success = true;

                // for each dimension
                for (int dimension = 0; dimension < regular.Rank; dimension++)
                {
                    var start = regular.Starts[dimension];
                    var stride = regular.Strides[dimension];
                    var count = regular.Counts[dimension];
                    var block = regular.Blocks[dimension];
                    var coordinate = coordinates[dimension];

                    if (coordinate < start)
                    {
                        success = false;
                        break;
                    }

                    var actualCount = (ulong)Math.DivRem((long)(coordinate - start), (long)stride, out var blockOffsetLong);
                    var blockOffset = (ulong)blockOffsetLong;

                    if (actualCount >= count || blockOffset >= block)
                    {
                        success = false;
                        break;
                    }

                    compactCoordinates[dimension] = actualCount * block + blockOffset;
                    maxCount = block - blockOffset;
                }
            }

            else
            {
                throw new NotSupportedException($"The hyperslab selection info of type {typeof(HyperslabSelectionInfo).Name} is not supported.");
            }

            if (success)
            {
                return new LinearIndexResult(
                    Success: true, 
                    LinearIndex: H5Utils.ToLinearIndex(compactCoordinates, SelectionInfo.CompactDimensions),
                    maxCount);
            }

            else
                return default;
        }

        public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
        {
            var coordinates = new ulong[SelectionInfo.Rank];
            var compactCoordinates = H5Utils.ToCoordinates(linearIndex, SelectionInfo.CompactDimensions);
            var success = false;
            ulong maxCount = default;

            // Expand compact coordinates
            if (SelectionInfo is IrregularHyperslabSelectionInfo irregular)
            {
                // For each block
                for (ulong blockIndex = 0; blockIndex < irregular.BlockCount; blockIndex++)
                {
                    success = true;
                    var offsetsGroupIndex = blockIndex * SelectionInfo.Rank;

                    // For each dimension
                    for (var dimension = 0; dimension < SelectionInfo.Rank; dimension++)
                    {
                        var dimensionIndex = (int)offsetsGroupIndex + dimension;
                        var compactCoordinate = compactCoordinates[dimension];

                        var start = irregular.BlockOffsets[dimensionIndex * 2];
                        var compactBlockStart = irregular.CompactBlockStarts[dimensionIndex];
                        var compactBlockEnd = irregular.CompactBlockEnds[dimensionIndex];

                        if (compactBlockStart <= compactCoordinate && compactCoordinate <= compactBlockEnd)
                        {
                            coordinates[dimension] = start + (compactCoordinate - compactBlockStart);
                            maxCount = compactBlockEnd - compactCoordinate + 1;
                        }
                        else
                        {
                            success = false;
                            break;
                        }
                    }

                    if (success)
                        break;
                }
            }

            else if (SelectionInfo is RegularHyperslabSelectionInfo regular)
            {
                success = true;

                // for each dimension
                for (int dimension = 0; dimension < regular.Rank; dimension++)
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
                }
            }

            else
            {
                throw new NotSupportedException($"The hyperslab selection info of type {typeof(HyperslabSelectionInfo).Name} is not supported.");
            }

            if (success)
                return new CoordinatesResult(Success: true, coordinates, maxCount);

            else
                return default;
        }

        #endregion
    }
}
