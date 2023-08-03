namespace PureHDF;

internal class H5DWrite
{
    private readonly H5DriverBase _driver;
    private readonly DataLayoutMessage4 _layout;

    public H5DWrite(H5DriverBase driver, DataLayoutMessage4 layout)
    {
        _driver = driver;
        _layout = layout;
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
            _driver.Seek((long)contiguous.Address, SeekOrigin.Begin);
            IH5WriteStream target = new OffsetStream(_driver);

            return Task.FromResult(target);
        }

        else if (_layout.Properties is ChunkedStoragePropertyDescription4 chunked)
        {
            if (chunked.IndexingTypeInformation is ImplicitIndexingInformation @implicit)
            {
                throw new NotImplementedException();
                // Seek((long)chunked.Address, SeekOrigin.Begin);
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