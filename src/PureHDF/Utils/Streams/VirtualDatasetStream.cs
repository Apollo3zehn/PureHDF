namespace PureHDF
{
    internal class VirtualDatasetStream : Stream
    {
        private record struct LinearIndexResult(bool Success, ulong LinearIndex, ulong MaxCount);

        private readonly VdsDatasetEntry[] _entries;
        private readonly ulong[] _dimensions;
        private readonly uint _typeSize;
        private long _position;

        public VirtualDatasetStream(VdsDatasetEntry[] entries, ulong[] dimensions, uint typeSize)
        {
            _entries = entries;
            _dimensions = dimensions;
            _typeSize = typeSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get => throw new NotImplementedException(); }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public override int Read(Span<byte> buffer)
        {
            return ReadCore(buffer);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
#else
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadCore(buffer.AsSpan(offset, count));
        }
#endif

        // public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        // {

        // }

        private int ReadCore(Span<byte> buffer)
        {
            // Overall algorithm:
            // - We get a linear byte index, which needs to be scaled by the type size.
            // - That index is then converted into coordinates which identify the position in the multidimensional virtual dataset.
            // - Now, try to find the VDS dataset entry which covers the requested slice.
            // - If no entry is found, use the fill value.
            // - If an entry was found: Use it to calculate the linear index with regards to the compact dimensions of the virtual selection.
            // - Additionally, determine the maximum number of subsequent elements which can be returned by the current entry
            // - Find the minimum of the current buffer length and the found maximum number of elements.
            // - Use the found linear index and convert it to coordinates using the compact dimensions of the source selection.
            // - Expand the compact coordinates to normal coordinates with regards to the source dataset.

            // TODO: Maybe there are useful performance improvements here: H5D__virtual_pre_io (H5Dvirtual.c)
            while (buffer.Length > 0)
            {
                // 1. Linear index to coordinates in virtual dataset
                var linearIndex = (ulong)_position / _typeSize;
                var coordinates = H5Utils.ToCoordinates(linearIndex, _dimensions);

                // 2. Calculate linear index and max count
                var result = default(LinearIndexResult);
                var foundEntry = default(VdsDatasetEntry);

                foreach (var entry in _entries)
                {
                    result = entry.VirtualSelection.SelectionInfo switch
                    {
                        H5S_SEL_NONE => default,
                        H5S_SEL_POINTS => throw new NotImplementedException(),
                        H5S_SEL_HYPER hyper => GetLinearIndex(hyper.HyperslabSelectionInfo, coordinates),
                        H5S_SEL_ALL => new LinearIndexResult(Success: true, linearIndex, MaxCount: _dimensions[^1] - coordinates[^1]),
                        _ => throw new NotSupportedException($"The selection of type {entry.VirtualSelection.SelectionType} is not supported.")
                    };

                    foundEntry = entry;

                    if (result.Success)
                        break;
                }

                // 3. Find min count (request vs virtual selection)
                var count = result.Success
                    ? Math.Min(buffer.Length / _typeSize, (long)result.MaxCount)
                    : buffer.Length / _typeSize;

                // if there is a source dataset with the requested data
                if (result.Success && foundEntry is not null)
                {
                    // // 4. Convert to coordinates of source selection
                    // var sourceCoordinates = foundEntry.SourceSelection.SelectionInfo switch
                    // {
                    //     H5S_SEL_NONE => throw new Exception("This should never happen!"),
                    //     H5S_SEL_POINTS => throw new NotImplementedException(),
                    //     H5S_SEL_HYPER hyper => GetCoordinates(hyper.HyperslabSelectionInfo, linearIndex),
                    //     H5S_SEL_ALL => throw new NotImplementedException(),
                    //     _ => throw new NotSupportedException($"The selection of type {foundEntry.SourceSelection.SelectionType} is not supported.")
                    // };

                    // 5. Read with expanded coordinates (hyperslab)
                }

                // else use the fill value
                else
                {
                    // fill value
                }



                // Update state
                var consumed = count * _typeSize;
                _position += consumed;
                buffer = buffer[(int)consumed..];
            }

            return buffer.Length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                _position = offset;
                return offset;
            }

            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        private static LinearIndexResult GetLinearIndex(HyperslabSelectionInfo selectionInfo, ulong[] coordinates)
        {
            var success = false;

            ulong maxCount = default;
            Span<ulong> compactCoordinates = stackalloc ulong[(int)selectionInfo.Rank];

            if (selectionInfo is HyperslabSelectionInfo1 info1)
            {
                // for each block
                for (uint blockIndex = 0; blockIndex < info1.BlockCount; blockIndex++)
                {
                    success = true;
                    var offsetsGroupIndex = blockIndex * info1.Rank;

                    // for each dimension
                    for (var dimension = 0; dimension < info1.Rank; dimension++)
                    {
                        var dimensionIndex = offsetsGroupIndex + dimension;
                        var start = info1.BlockOffsets[dimensionIndex * 2 + 0];
                        var end = info1.BlockOffsets[dimensionIndex * 2 + 1];
                        var coordinate = coordinates[dimension];

                        if (start <= coordinate && coordinate <= end)
                        {
                            var compactStart = info1.CompactBlockCoordinates[dimensionIndex];

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

            else if (selectionInfo is HyperslabSelectionInfo2 info2)
            {
                success = true;

                // for each dimension
                for (int dimension = 0; dimension < info2.Rank; dimension++)
                {
                    var start = info2.Starts[dimension];
                    var stride = info2.Strides[dimension];
                    var count = info2.Counts[dimension];
                    var block = info2.Blocks[dimension];
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
                    maxCount = blockOffset - block;
                }
            }

            else if (selectionInfo is HyperslabSelectionInfo3 _)
            {
                throw new NotImplementedException("Hyperslab selection info v3 is not implemented.");
            }

            else
            {
                throw new NotSupportedException($"The hyperslab selection info of type {typeof(HyperslabSelectionInfo).Name} is not supported.");
            }

            if (success)
            {
                var linearIndex = H5Utils.ToLinearIndex(compactCoordinates, selectionInfo.CompactDimensions);
                return new LinearIndexResult(Success: true, linearIndex, maxCount);
            }

            return default;
        }

        // private static ulong[] GetCoordinates(HyperslabSelectionInfo selectionInfo, ulong linearIndex)
        // {
        //     var compactCoordinates = H5Utils.ToCoordinates(linearIndex, selectionInfo.CompactDimensions);
        // }
    }
}
