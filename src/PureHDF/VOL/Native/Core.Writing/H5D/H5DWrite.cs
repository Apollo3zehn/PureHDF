namespace PureHDF;

internal class H5DWrite
{
    private readonly WriteContext _context;
    private readonly DataLayoutMessage4 _layout;
    private readonly H5Dataset _dataset;
    private readonly IChunkCache? _chunkCache;

    public H5DWrite(WriteContext context, H5Dataset dataset, DataLayoutMessage4 layout)
    {
        _context = context;
        _layout = layout;
        _dataset = dataset;

        if (dataset.ChunkDimensions is not null)
            _chunkCache = dataset.DatasetAccess.ChunkCache ?? context.File.ChunkCacheFactory();
    }

    public virtual Task<IH5WriteStream> GetWriteStreamAsync<TReader>(
        TReader reader, 
        ulong[] chunkIndices) where TReader : IReader
    {
        if (_layout.Properties is CompactStoragePropertyDescription compact)
        {
            IH5WriteStream target = new SystemMemoryStream(compact.InputData);
            return Task.FromResult(target);
        }

        else if (_layout.Properties is ContiguousStoragePropertyDescription contiguous)
        {
            _context.Driver.Seek((long)contiguous.Address, SeekOrigin.Begin);
            IH5WriteStream target = new OffsetStream(_context.Driver);

            return Task.FromResult(target);
        }

        else if (_layout.Properties is ChunkedStoragePropertyDescription4 chunked)
        {
            if (chunked.IndexingTypeInformation is ImplicitIndexingInformation @implicit)
            {
                // var chunk = _chunkCache!
                //     .GetChunkAsync(
                //         chunkIndices, 
                //         () => 
                //         {
                //             var address = chunked.Address + ;
                //             _context.Driver.Seek(address, SeekOrigin.Begin);

                //             var buffer = ;
                //             _context.Driver.ReadDataset(buffer);
                //         }, 
                //         (writeIndices, writeChunk) => throw new NotImplementedException())
                //     .ConfigureAwait(false)
                //     // TODO this is not clean
                //     .GetAwaiter()
                //     .GetResult();

                throw new Exception();
            }

            else
            {
                throw new Exception($"The indexing type {chunked.IndexingTypeInformation.GetType()} is not supported.");
            }
        }

        else
        {
            throw new Exception($"The data layout {_layout.GetType().Name} is not supported.");
        }
    }
}