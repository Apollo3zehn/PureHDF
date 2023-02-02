﻿using System.Buffers;

namespace PureHDF
{
    internal class VirtualDatasetStream : Stream
    {
        private record class DatasetInfo(H5File File, H5Dataset Dataset, H5DatasetAccess DatasetAccess);

        private long _position;
        private readonly uint _typeSize;
        private readonly ulong[] _virtualDimensions;
        private readonly byte[]? _fillValue;
        private readonly H5DatasetAccess _datasetAccess;
        private readonly VdsDatasetEntry[] _entries;
        private readonly Dictionary<VdsDatasetEntry, DatasetInfo> _datasetInfoMap = new();

        public VirtualDatasetStream(
            VdsDatasetEntry[] entries, 
            ulong[] dimensions, 
            uint typeSize, 
            byte[]? fillValue,
            H5DatasetAccess datasetAccess)
        {
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
                var virtualCoordinates = H5Utils.ToCoordinates(virtualLinearIndex, _virtualDimensions);

                // 2. Calculate linear index and max count
                var virtualResult = default(LinearIndexResult);
                var sourceSelection = default(DataspaceSelection);
                var sourceDatasetInfo = default(DatasetInfo);

                foreach (var entry in _entries)
                {
                    virtualResult = entry.VirtualSelection.Info
                        .ToLinearIndex(_virtualDimensions, virtualCoordinates);

                    sourceSelection = entry.SourceSelection;
                    sourceDatasetInfo = GetDatasetInfo(entry);

                    if (virtualResult.Success)
                        break;
                }

                // 4. Find min count (request vs virtual selection)
                var scaledBufferLength = (ulong)buffer.Length / _typeSize;

                ulong virtualCount = virtualResult.Success
                    ? Math.Min(scaledBufferLength, virtualResult.MaxCount)
                    : scaledBufferLength;

                var virtualByteCount = (long)virtualCount * _typeSize;

                // 5. Read data
                var slicedBuffer = buffer[..(int)virtualByteCount];

                // From source dataset
                if (virtualResult.Success && 
                    sourceSelection is not null && 
                    sourceDatasetInfo is not null)
                {
                    var selection = new DelegateSelection(
                        virtualCount, 
                        sourceDimensions => Walker(
                            virtualCount, 
                            virtualResult.LinearIndex, 
                            sourceDimensions, 
                            sourceSelection));

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

            return buffer.Length;
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
                var filePath = H5Utils.ConstructExternalFilePath(entry.SourceFileName, _datasetAccess);

                if (File.Exists(filePath))
                {
                    // TODO: check how file should be opened
                    var file = H5File.OpenRead(filePath);

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