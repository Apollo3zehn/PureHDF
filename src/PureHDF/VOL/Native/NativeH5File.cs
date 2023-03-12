namespace PureHDF;

internal class NativeH5File : H5Group, IH5File
{
    // TODO: K-Values message https://forum.hdfgroup.org/t/problem-reading-version-1-8-hdf5-files-using-file-format-specification-document-clarification-needed/7568
    #region Fields

    private readonly bool _deleteOnClose;
    private Func<IChunkCache>? _chunkCacheFactory;

    #endregion

    #region Constructors

    private NativeH5File(
        H5Context context,
        NamedReference reference,
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

    internal static NativeH5File OpenRead(string filePath, bool deleteOnClose = false)
    {
        return Open(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            useAsync: false,
            deleteOnClose: deleteOnClose);
    }

    internal static NativeH5File Open(
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

    internal static NativeH5File Open(H5DriverBase driver, string absoluteFilePath, bool deleteOnClose = false)
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
            0 => new Superblock01(driver, version),
            1 => new Superblock01(driver, version),
            2 => new Superblock23(driver, version),
            3 => new Superblock23(driver, version),
            _ => throw new NotSupportedException($"The superblock version '{version}' is not supported.")
        });

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
        var context = new H5Context(driver, superblock);
        var header = ObjectHeader.Construct(context);

        var file = new NativeH5File(context, default, header, absoluteFilePath, deleteOnClose);
        var reference = new NamedReference("/", address, file);
        file.Reference = reference;

        return file;
    }

    private static bool ValidateSignature(byte[] actual, byte[] expected)
    {
        if (actual.Length == expected.Length)
        {
            if (actual[0] == expected[0] && actual[1] == expected[1] && actual[2] == expected[2] && actual[3] == expected[3]
             && actual[4] == expected[4] && actual[5] == expected[5] && actual[6] == expected[6] && actual[7] == expected[7])
            {
                return true;
            }
        }

        return false;
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
                H5Cache.Clear(Context.Superblock);
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