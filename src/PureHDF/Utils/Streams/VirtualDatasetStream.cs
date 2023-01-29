namespace PureHDF
{
    internal class VirtualDatasetStream : Stream
    {
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

        public override int Read(Span<byte> buffer)
        {
            // 1. linear index to coordinates in virtual dataset
            var scaledIndex = (ulong)_position / _typeSize;
            var coordinates = H5Utils.ToCoordinates(scaledIndex, _dimensions);

            // 2. find matching VirtualDatasetEntry
            ulong maxCount = default;

            var entry = _entries.FirstOrDefault(entry =>
            {
                var success = entry.VirtualSelection.SelectionInfo switch
                {
                    H5S_SEL_NONE none => false,
                    H5S_SEL_POINTS points => throw new NotImplementedException(),
                    H5S_SEL_HYPER hyper => IsMatch(hyper.HyperslabSelectionInfo, coordinates, out maxCount),
                    H5S_SEL_ALL all => true,
                    _ => throw new NotSupportedException($"The selection of type {entry.VirtualSelection.SelectionType} is not supported.")
                };

                return success;
            });

            // 3. find min length (entry vs request)
            var count = entry is null
                ? buffer.Length
                : Math.Min(buffer.Length, (long)maxCount);

            // - if virtual dimensions != file dimensions
            // - compact coordinates, 
            // - convert to linear
            // - convert to file dimensionslose
            // - expand
            // - read with expanded coordinates (hyperslab)


            // TODO: now find out which VdsDataSetEntry holds these data, if none: fill value.
            // - There are two kinds to hyperslab selections (and other selection types, too).
            // - For hyper selection type 1: use binary search to quickly find block.

            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        // public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        // {
            
        // }

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

        private static bool IsMatch(HyperslabSelectionInfo selectionInfo, ulong[] coordinates, out ulong maxCount)
        {
            maxCount = default;

            // TODO: is there a more efficient way?
            if (selectionInfo is HyperslabSelectionInfo1 info1)
            {
                var totalBlockCount = info1.BlockCount * info1.Rank;

                // for each block
                for (int blockIndex = 0; blockIndex < totalBlockCount; blockIndex+=(int)info1.Rank)
                {
                    var result = true;

                    // for each dimension
                    for (int dimension = 0; dimension < info1.Rank; dimension+=2)
                    {
                        var start = info1.BlockOffsets[blockIndex + dimension + 0];
                        var end = info1.BlockOffsets[blockIndex + dimension + 1];
                        var coordinate = coordinates[dimension];

                        if (!(start <= coordinate && coordinate <= end))
                        {
                            result = false;
                            break;
                        }
                    }

                    if (result)
                        return true; // TODO: return block index
                }
            }

            else if (selectionInfo is HyperslabSelectionInfo2 info2)
            {
                var result = true;

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
                        result = false;
                        break;
                    }

                    var actualCount = Math.DivRem((long)(coordinate - start), (long)stride, out var actualBlock);

                    if (actualCount >= (long)count || actualBlock >= (long)block)
                    {
                        result = false;
                        break;
                    }

                    maxCount = (ulong)actualBlock - block;
                }

                return result;
            }

            else
            {
                throw new NotSupportedException($"The hyperslab selection info of type {typeof(HyperslabSelectionInfo).Name} is not supported.");
            }

            return false;
        }
    }
}
