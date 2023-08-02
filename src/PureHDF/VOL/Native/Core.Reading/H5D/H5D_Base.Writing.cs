namespace PureHDF
{
    internal abstract partial class H5D_Base : IDisposable
    {
        public virtual Task<IH5WriteStream> GetWriteStreamAsync<TReader>(TReader reader, ulong[] chunkIndices) where TReader : IReader
        {
            throw new NotImplementedException();
        }
    }
}