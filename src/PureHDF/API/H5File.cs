using System.IO.MemoryMappedFiles;

namespace PureHDF
{
    /// <summary>
    /// An HDF5 file object. This is the entry-point to work with HDF5 files.
    /// </summary>
    public sealed partial class H5File : H5Group, IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets the path of the opened HDF5 file if loaded from the file system.
        /// </summary>
        public string? Path { get; }

        /// <summary>
        /// Gets or sets the current chunk cache factory.
        /// </summary>
        public Func<IChunkCache> ChunkCacheFactory
        {
            get
            {
                if (_chunkCacheFactory is not null)
                    return _chunkCacheFactory;

                else
                    return DefaultChunkCacheFactory;
            }
            set
            {
                _chunkCacheFactory = value;
            }
        }

        /// <summary>
        /// The default chunk cache factory.
        /// </summary>
        public static Func<IChunkCache> DefaultChunkCacheFactory { get; } = () => new SimpleChunkCache();

        #endregion

        #region Methods

        /// <summary>
        /// Opens an HDF5 file for reading. Please see the <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.io.file.openread#remarks">Remarks</seealso> section for more information how the file is opened.
        /// </summary>
        /// <param name="filePath">The file to open.</param>
        public static H5File OpenRead(string filePath)
        {
            return OpenReadCore(filePath);
        }

        /// <summary>
        /// Opens an HDF5 file.
        /// </summary>
        /// <param name="filePath">The file to open.</param>
        /// <param name="mode">A <see cref="FileMode"/> value that specifies whether a file is created if one does not exist, and determines whether the contents of existing files are retained or overwritten.</param>
        /// <param name="fileAccess">A <see cref="FileAccess"/> value that specifies the operations that can be performed on the file.</param>
        /// <param name="fileShare">A <see cref="FileShare"/> value specifying the type of access other threads have to the file.</param>
        /// <param name="useAsync">A boolean which indicates if the file be opened with the <see cref="FileOptions.Asynchronous"/> flag.</param>
        public static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare, bool useAsync = false)
        {
            return OpenCore(filePath, mode, fileAccess, fileShare, useAsync: useAsync);
        }

        /// <summary>
        /// Opens an HDF5 stream.
        /// </summary>
        /// <param name="stream">The stream to use.</param>
        /// <param name="leaveOpen">A boolean which indicates if the stream should be kept open when this class is disposed. The default is <see langword="false"/>.</param>
        /// <returns></returns>
        public static H5File Open(Stream stream, bool leaveOpen = false)
        {
            H5BaseReader reader;

#if NET6_0_OR_GREATER
            if (stream is FileStream fileStream)
                reader = new H5FileStreamReader(fileStream, leaveOpen: leaveOpen);

            else
#endif
                reader = new H5StreamReader(stream, leaveOpen: leaveOpen);

            return OpenCore(reader, string.Empty);
        }

        /// <summary>
        /// Opens an HDF5 memory-mapped file.
        /// </summary>
        /// <param name="accessor">The memory-mapped accessor to use.</param>
        /// <returns></returns>
        public static H5File Open(MemoryMappedViewAccessor accessor)
        {
            var reader = new H5MemoryMappedFileReader(accessor);
            return OpenCore(reader, string.Empty);
        }

        /// <inheritdoc />
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
