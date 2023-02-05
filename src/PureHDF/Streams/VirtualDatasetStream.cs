using System.Buffers;

namespace PureHDF
{
    internal class VirtualDatasetStream : Stream
    {
        private record class DatasetInfo(H5File File, H5Dataset Dataset, H5DatasetAccess DatasetAccess);

        private long _position;
        private readonly uint _typeSize;
        private readonly ulong[] _virtualDimensions;
        private readonly byte[]? _fillValue;
        private readonly H5File _file;
        private readonly H5DatasetAccess _datasetAccess;
        private readonly VdsDatasetEntry[] _entries;
        private readonly Dictionary<VdsDatasetEntry, DatasetInfo> _datasetInfoMap = new();

        public VirtualDatasetStream(
            H5File file,
            VdsDatasetEntry[] entries, 
            ulong[] dimensions, 
            uint typeSize, 
            byte[]? fillValue,
            H5DatasetAccess datasetAccess)
        {
            _file = file;
            _entries = entries;
            _virtualDimensions = dimensions;
            _typeSize = typeSize;
            _fillValue = fillValue;
            _datasetAccess = datasetAccess;
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

#region Methods

        private int ReadCore(Memory<byte> buffer)
        {
            var length = buffer.Length;

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
                var virtualLinearIndex = (ulong)_position / _typeSize;
                var virtualCoordinates = Utils.ToCoordinates(virtualLinearIndex, _virtualDimensions);

                // 2. Calculate linear index and max count
                var virtualResult = default(LinearIndexResult);
                var foundEntry = default(VdsDatasetEntry);
                var minimumMaxCount = default(ulong);

                foreach (var entry in _entries)
                {
                    virtualResult = entry.VirtualSelection.Info
                        .ToLinearIndex(_virtualDimensions, virtualCoordinates);

                    // we found a suitable selection
                    if (virtualResult.Success)
                    {
                        foundEntry = entry;
                        minimumMaxCount = virtualResult.MaxCount;
                        break;
                    }
                    // continue searching for the minimum distance to the next selection
                    else if (virtualResult.MaxCount != 0)
                    {
                        if (minimumMaxCount == default || virtualResult.MaxCount < minimumMaxCount)
                            minimumMaxCount = virtualResult.MaxCount;
                    }
                }

                // 4. Find min count (request vs virtual selection)
                var scaledBufferLength = (ulong)buffer.Length / _typeSize;

                ulong virtualCount = virtualResult.MaxCount == 0
                    // MaxCount == 0: there is no block in the fastest changing dimension
                    ? scaledBufferLength
                    // MaxCount != 0: 
                    //  - block width (virtualResult.Success == true) or 
                    //  - distance until next block begins (virtualResult.Success == false)
                    : Math.Min(scaledBufferLength, minimumMaxCount); // 

                var virtualByteCount = (long)virtualCount * _typeSize;

                // 5. Read data
                var slicedBuffer = buffer[..(int)virtualByteCount];

                // From source dataset
                var sourceDatasetInfo = default(DatasetInfo);

                if (foundEntry is not null)
                    sourceDatasetInfo = GetDatasetInfo(foundEntry);

                if (sourceDatasetInfo is not null && foundEntry is not null)
                {
                    var selection = new DelegateSelection(
                        virtualCount, 
                        sourceDimensions => Walker(
                            virtualCount, 
                            virtualResult.LinearIndex, 
                            sourceDimensions, 
                            foundEntry.SourceSelection));

                    sourceDatasetInfo.Dataset
                        .Read(slicedBuffer, selection, datasetAccess: sourceDatasetInfo.DatasetAccess);
                }

                // Fill value
                else
                {
                    if (_fillValue is not null)
                        slicedBuffer.Span.Fill(_fillValue);

                    else
                        slicedBuffer.Span.Fill(0);
                }

                // Update state
                _position += virtualByteCount;
                buffer = buffer[(int)virtualByteCount..];
            }

            return length;
        }

        private static IEnumerable<Step> Walker(
            ulong totalElementCount, 
            ulong linearIndex, 
            ulong[] sourceDimensions,
            DataspaceSelection sourceSelection)
        {
            var remaining = totalElementCount;

            while (remaining > 0)
            {
                var (success, coordinates, sourceMaxCount) = sourceSelection.Info
                    .ToCoordinates(sourceDimensions, linearIndex);

                if (!success)
                    throw new Exception("This should never happen.");

                var sourceCount = Math.Min(remaining, sourceMaxCount);

                // Update state
                remaining -= sourceCount;
                linearIndex += sourceCount;

                yield return new Step() 
                { 
                    Coordinates = coordinates, 
                    ElementCount = sourceCount 
                };
            }
        }

        private DatasetInfo? GetDatasetInfo(VdsDatasetEntry entry)
        {
            if (!_datasetInfoMap.TryGetValue(entry, out var info))
            {
                var filePath = FilePathUtils.FindVirtualFile(_file.Path, entry.SourceFileName, _datasetAccess);

                if (filePath is not null)
                {
                    // TODO: check how file should be opened
                    var file = filePath == "."
                        // this file
                        ? _file
                        // external file
                        : H5File.OpenRead(filePath);

                    // TODO: Where to get link access from? From the virtual dataset?
                    if (file.LinkExists(entry.SourceDataset, linkAccess: default))
                    {
                        var datasetAccess = _datasetAccess;

                        if (_datasetAccess.ChunkCacheFactory is null)
                            datasetAccess = _datasetAccess with { ChunkCacheFactory = () => new SimpleChunkCache() };

                        var dataset = file.Dataset(entry.SourceDataset);

                        info = new DatasetInfo(file, dataset, datasetAccess);
                        _datasetInfoMap[entry] = info;
                    }
                }
            }

            return info;
        }

#endregion

#region Stream

        public override void Flush()
        {
            throw new NotImplementedException();
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public override int Read(Span<byte> buffer)
        {
            // TODO: Avoidable? Just do not implement this Read overload?
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(buffer.Length);
            var memory = memoryOwner.Memory[..buffer.Length];
            var readCount = ReadCore(memory);

            memory.Span.CopyTo(buffer);

            return readCount;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
#else
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadCore(buffer.AsMemory(offset, count));
        }
#endif

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
            foreach (var datasetInfo in _datasetInfoMap.Values)
            {
                try
                {
                    // do not close this file 
                    if (datasetInfo.File != _file)
                        datasetInfo.File.Dispose();
                }
                catch
                {
                    //
                }
            }

            base.Dispose(disposing);
        }

#endregion
    }
}
