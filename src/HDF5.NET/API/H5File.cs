namespace HDF5.NET
{
    public partial class H5File : H5Group, IDisposable
    {
        #region Properties

        public string Path { get; } = ":memory:";

        public Func<IChunkCache> ChunkCacheFactory
        {
            get
            {
                if (_chunkCacheFactory is not null)
                    return _chunkCacheFactory;

                else
                    return H5File.DefaultChunkCacheFactory;
            }
            set
            {
                _chunkCacheFactory = value;
            }
        }

        public static Func<IChunkCache> DefaultChunkCacheFactory = () => new SimpleChunkCache();

        #endregion

        #region Methods

        public static H5File OpenRead(string filePath)
        {
            return H5File.OpenReadCore(filePath);
        }

        public static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare, bool useAsync = false)
        {
            return H5File.OpenCore(filePath, mode, fileAccess, fileShare, useAsync: useAsync);
        }

        public static H5File Open(Stream stream)
        {
            return H5File.OpenCore(stream, string.Empty);
        }

        public void Dispose()
        {
            H5Cache.Clear(Context.Superblock);
            Context.Reader.Dispose();

            if (_deleteOnClose && System.IO.File.Exists(Path))
            {
                try
                {
                    System.IO.File.Delete(Path);
                }
                catch
                {
                    //
                }
            }    
        }

        #endregion
    }
}
