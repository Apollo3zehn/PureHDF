namespace PureHDF
{
    internal delegate Task ReadVirtualDelegate<TResult>(H5Dataset dataset, Memory<TResult> destination, Selection fileSelection, H5DatasetAccess datasetAccess);

    internal class VirtualDatasetStream<TResult> : IH5ReadStream
    {
        private record class DatasetInfo(H5File File, H5Dataset Dataset, H5DatasetAccess DatasetAccess);

        private long _position;
        private readonly ulong[] _virtualDimensions;
        private readonly TResult? _fillValue;
        private readonly H5File _file;
        private readonly H5DatasetAccess _datasetAccess;
        private readonly VdsDatasetEntry[] _entries;
        private readonly Dictionary<VdsDatasetEntry, DatasetInfo> _datasetInfoMap = new();
        private readonly ReadVirtualDelegate<TResult> _readVirtual;

        public VirtualDatasetStream(
            H5File file,
            VdsDatasetEntry[] entries, 
            ulong[] dimensions, 
            TResult? fillValue,
            H5DatasetAccess datasetAccess,
            ReadVirtualDelegate<TResult> readVirtual)
        {
            _file = file;
            _entries = entries;
            _virtualDimensions = dimensions;
            _fillValue = fillValue;
            _datasetAccess = datasetAccess;
            _readVirtual = readVirtual;
        }

        public long Position { get => _position; }

        public void Read(Memory<byte> buffer) => throw new NotImplementedException();

        public async ValueTask ReadVirtualAsync(Memory<TResult> buffer)
        {
            // Overall algorithm:
            // - We get a linear index.
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
                var virtualLinearIndex = (ulong)_position;
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
                var virtualCount = virtualResult.MaxCount == 0
                    // MaxCount == 0: there is no block in the fastest changing dimension
                    ? (ulong)buffer.Length
                    // MaxCount != 0: 
                    //  - block width (virtualResult.Success == true) or 
                    //  - distance until next block begins (virtualResult.Success == false)
                    : Math.Min((ulong)buffer.Length, minimumMaxCount); // 

                // 5. Read data
                var slicedBuffer = buffer[..(int)virtualCount];

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

                    await _readVirtual(
                        dataset: sourceDatasetInfo.Dataset, 
                        destination: slicedBuffer, 
                        fileSelection: selection, 
                        datasetAccess: sourceDatasetInfo.DatasetAccess);
                }

                // Fill value
                else
                {
                    if (_fillValue is not null)
                        slicedBuffer.Span.Fill(_fillValue);

                    else
                        slicedBuffer.Span.Clear();
                }

                // Update state
                _position += (long)virtualCount;
                buffer = buffer[(int)virtualCount..];
            }
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
                var (coordinates, sourceMaxCount) = sourceSelection.Info
                    .ToCoordinates(sourceDimensions, linearIndex);

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
                var filePath = FilePathUtils.FindExternalFileForVirtualDataset(_file.FolderPath, entry.SourceFileName, _datasetAccess);

                if (filePath is not null)
                {
                    // TODO: File should be opened asynchronously if this file is also opened asynchronously.
                    var file = filePath == "."
                        // this file
                        ? _file
                        // external file
                        : H5File.OpenRead(filePath);

                    if (file.LinkExists(entry.SourceDataset, linkAccess: default /* no link access available */))
                    {
                        var datasetAccess = _datasetAccess;
                        var chunkCache = new SimpleChunkCache();

                        if (_datasetAccess.ChunkCacheFactory is null)
                            datasetAccess = _datasetAccess with { ChunkCacheFactory = () => chunkCache };

                        var dataset = file.Dataset(entry.SourceDataset);

                        info = new DatasetInfo(file, dataset, datasetAccess);
                        _datasetInfoMap[entry] = info;
                    }
                }
            }

            return info;
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
                _position = offset;

            throw new NotImplementedException();
        }

#region IDisposable

        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
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
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

#endregion
}
