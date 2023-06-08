using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal class NativeFile : NativeGroup, INativeFile
{
    // TODO: K-Values message https://forum.hdfgroup.org/t/problem-reading-version-1-8-hdf5-files-using-file-format-specification-document-clarification-needed/7568
    #region Fields

    private readonly bool _deleteOnClose;
    private Func<IChunkCache>? _chunkCacheFactory;

    #endregion

    #region Constructors

    private NativeFile(
        NativeContext context,
        NativeNamedReference reference,
        ObjectHeader header,
        string absoluteFilePath,
        bool deleteOnClose) : base(context, reference, header)
    {
        Path = absoluteFilePath;
        FolderPath = System.IO.Path.GetDirectoryName(absoluteFilePath);
        _deleteOnClose = deleteOnClose;
    }

    #endregion

    #region Properties

    public string? Path { get; }

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

    internal string? FolderPath { get; }

    #endregion

    #region Methods

    internal static INativeFile OpenRead(string filePath, bool deleteOnClose = false)
    {
        return Open(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            useAsync: false,
            deleteOnClose: deleteOnClose);
    }

    internal static INativeFile Open(
        string filePath,
        FileMode fileMode,
        FileAccess fileAccess,
        FileShare fileShare,
        bool useAsync = false,
        bool deleteOnClose = false)
    {
        var absoluteFilePath = System.IO.Path.GetFullPath(filePath);

        var stream = new FileStream(
            absoluteFilePath,
            fileMode,
            fileAccess,
            fileShare,
            4096,
            useAsync);

#if NET6_0_OR_GREATER
        var driver = new H5FileHandleDriver(stream, leaveOpen: false);
#else
        var driver = new H5StreamDriver(stream, leaveOpen: false);
#endif

        return Open(driver, absoluteFilePath, deleteOnClose);
    }

    internal static INativeFile Open(H5DriverBase driver, string absoluteFilePath, bool deleteOnClose = false)
    {
        if (!BitConverter.IsLittleEndian)
            throw new Exception("This library only works on little endian systems.");

        // superblock
        var stepSize = 512;
        var signature = driver.ReadBytes(8);

        while (!ValidateSignature(signature, Superblock.FormatSignature))
        {
            driver.Seek(stepSize - 8, SeekOrigin.Current);

            if (driver.Position >= driver.Length)
                throw new Exception("The file is not a valid HDF 5 file.");

            signature = driver.ReadBytes(8);
            stepSize *= 2;
        }

        var version = driver.ReadByte();

        var superblock = (Superblock)(version switch
        {
            >= 0 and < 2 => Superblock01.Decode(driver, version),
            >= 2 and < 4 => Superblock23.Decode(driver, version),
            _ => throw new NotSupportedException($"The superblock version '{version}' is not supported.")
        });

        #if ANONYMIZE
            AnonymizeHelper.Append("offset", absoluteFilePath, (long)superblock.BaseAddress, 0, addBaseAddress: default);
        #endif

        superblock.FilePath = absoluteFilePath;

        driver.SetBaseAddress(superblock.BaseAddress);

        ulong address;
        var superblock01 = superblock as Superblock01;

        if (superblock01 is not null)
        {
            address = superblock01.RootGroupSymbolTableEntry.HeaderAddress;
        }
        
        else
        {
            var superblock23 = superblock as Superblock23;

            if (superblock23 is not null)
                address = superblock23.RootGroupObjectHeaderAddress;

            else
                throw new Exception($"The superblock of type '{superblock.GetType().Name}' is not supported.");
        }

        driver.Seek((long)address, SeekOrigin.Begin);
        var context = new NativeContext(driver, superblock);
        var header = ObjectHeader.Construct(context);

        var file = new NativeFile(context, default, header, absoluteFilePath, deleteOnClose);
        var reference = new NativeNamedReference("/", address, file);
        file.Reference = reference;

        return file;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ValidateSignature(byte[] actual, byte[] expected)
    {
        return expected.SequenceEqual(actual);
    }

#endregion

#region IDisposable

    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                NativeCache.Clear(Context.Driver);
                Context.Driver.Dispose();

                if (_deleteOnClose && System.IO.File.Exists(Path))
                {
                    try
                    {
                        System.IO.File.Delete(Path!);
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

#endregion

}