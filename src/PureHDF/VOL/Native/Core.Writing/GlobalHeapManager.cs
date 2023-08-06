namespace PureHDF.VOL.Native;

internal class GlobalHeapManager
{
    private const int ALIGNMENT = 8;
    private const int MINIMUM_COLLECTION_SIZE = 4096; /* according to spec, includes collection header */
    private const int COLLECTION_HEADER_SIZE = 16;
    private const int OBJECT_HEADER_SIZE = 16;

    private readonly int _totalCollectionSize;
    private readonly int _collectionSize;
    private readonly long _flushThreshold;
    private readonly FreeSpaceManager _freeSpaceManager;
    private readonly Dictionary<long, GlobalHeapCollectionState> _collectionMap = new();
    private readonly H5DriverBase _driver;

    private GlobalHeapCollectionState? _collectionState;
    private long _baseAddress;
    private ushort _index;
    private Memory<byte> _memory;

    public GlobalHeapManager(H5WriteOptions options, FreeSpaceManager freeSpaceManager, H5DriverBase driver)
    {
        if (options.GlobalHeapCollectionSize < MINIMUM_COLLECTION_SIZE)
            throw new Exception($"The minimum global heap collection size is {MINIMUM_COLLECTION_SIZE} bytes");

        _totalCollectionSize = options.GlobalHeapCollectionSize;
        _collectionSize = _totalCollectionSize - COLLECTION_HEADER_SIZE;
        _flushThreshold = options.GlobalHeapFlushThreshold;
        _freeSpaceManager = freeSpaceManager;
        _driver = driver;
    }

    public (WritingGlobalHeapId, IH5WriteStream) AddObject(int size)
    {
        // validation
        if (_collectionState is null)
            AddNewCollection();

        var collectionState = _collectionState!;

        if (collectionState.Consumed + size > collectionState.Memory.Length)
        {
            if (size > collectionState.Memory.Length)
                throw new Exception("The object is too large for the global object heap.");

            else
                AddNewCollection();
        }

        // encode object header
        _index++;

        BitConverter
            .GetBytes(_index)
            .CopyTo(_memory.Span.Slice(collectionState.Consumed, sizeof(ushort)));

        collectionState.Consumed += sizeof(ushort);

        BitConverter
            .GetBytes((ushort)1)
            .CopyTo(_memory.Span.Slice(collectionState.Consumed, sizeof(ushort)));

        collectionState.Consumed += sizeof(ushort);
        collectionState.Consumed += 4;

        BitConverter
            .GetBytes((ulong)size)
            .CopyTo(_memory.Span.Slice(collectionState.Consumed, sizeof(ulong)));

        collectionState.Consumed += sizeof(ulong);

        var globalHeapId = new WritingGlobalHeapId(
            Address: (ulong)_baseAddress,
            Index: _index
        );

        // object data
        var data = _memory.Slice(collectionState.Consumed, size);

        /* H5HGpkg.h #define H5HG_ALIGN(X) */
        var alignedSize = ALIGNMENT * ((size + ALIGNMENT - 1) / ALIGNMENT);
        collectionState.Consumed += alignedSize;

        return (
            globalHeapId,
            // TODO this is a huge performance problem when there are many var length objects
            // reuse the stream did not work because of nested Global Heap Id requests
            new SystemMemoryStream(data)
        );
    }

    private void AddNewCollection()
    {
        // flush before we are able to continue
        if (_collectionMap.Count * _collectionSize >= _flushThreshold)
        {
            Encode();
            _collectionMap.Clear();
        }

        // TODO make encoding and decoding of collection more symmetrical

        var collection = new GlobalHeapCollection(default!)
        {
            Version = 1,
            CollectionSize = (ulong)_totalCollectionSize
        };

        _baseAddress = _freeSpaceManager.Allocate(_totalCollectionSize);
        _memory = new byte[_collectionSize];

        //
        _index = 0;

        var collectionState = new GlobalHeapCollectionState(
            Collection: collection,
            Memory: _memory);

        _collectionState = collectionState;
        _collectionMap[_baseAddress] = collectionState;
    }

    public void Encode()
    {
        var driver = _driver;

        foreach (var entry in _collectionMap)
        {
            var address = entry.Key;
            var (collection, memory) = entry.Value;
            var consumed = entry.Value.Consumed;
            var remainingSpace = (ulong)(memory.Length - consumed);

            driver.Seek(address, SeekOrigin.Begin);

            // signature
            driver.Write(GlobalHeapCollection.Signature);

            // version
            driver.Write(collection.Version);

            // reserved
            driver.Seek(3, SeekOrigin.Current);

            // collection size
            driver.Write(collection.CollectionSize);

            // collection
            driver.Write(memory.Span[..consumed]);

            // Global Heap Object 0
            if (remainingSpace > OBJECT_HEADER_SIZE)
            {
                /* The field Object Size for Object 0 indicates the amount of possible free space 
                   in the collection INCLUDING the 16-byte header size of Object 0.  */
                driver.Seek(sizeof(ushort) + sizeof(ushort) + 4, SeekOrigin.Current);
                driver.Write(remainingSpace);
                remainingSpace -= OBJECT_HEADER_SIZE;
            }

            var endAddress = driver.Position + (long)remainingSpace;

            if (driver.Length < endAddress)
                driver.SetLength(endAddress);
        }
    }
}