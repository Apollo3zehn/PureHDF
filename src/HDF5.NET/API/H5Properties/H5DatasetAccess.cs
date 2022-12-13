namespace HDF5.NET
{
    public struct H5DatasetAccess
    {
        public string ExternalFilePrefix { get; init; }

        public Func<IChunkCache> ChunkCacheFactory { get; init; }
    }
}
