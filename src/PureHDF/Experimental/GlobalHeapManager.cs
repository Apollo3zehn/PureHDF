namespace PureHDF.Experimental;

internal class GlobalHeapManager
{
    private const int COLLECTION_SIZE = 4096;
    private const int COLLECTION_HEADER_SIZE = 16;

    private readonly FreeSpaceManager _freeSpaceManager;
    private readonly Dictionary<ulong, (GlobalHeapCollection, Memory<byte>)> _collectionMap = new();
    private GlobalHeapCollection? _collection;
    private ulong _baseAddress;
    private ushort _index;
    private int _consumed;
    private Memory<byte> _memory;

    public GlobalHeapManager(FreeSpaceManager freeSpaceManager)
    {
        _freeSpaceManager = freeSpaceManager;
    }

    public (GlobalHeapId, Memory<byte>) AddObject(int size)
    {
        // validation
        if (_collection is null)
            AddNewCollection();

        var collection = _collection!.Value;

        if (_consumed + size > (int)collection.CollectionSize)
        {
            if (size > (int)collection.CollectionSize)
                throw new Exception("The object is too large for the global object heap.");

            else
                AddNewCollection();
        }

        // encode object header
        _index++;

        BitConverter
            .GetBytes(_index)
            .CopyTo(_memory.Span.Slice(_consumed, sizeof(ushort)));

        _consumed += sizeof(ushort);

        BitConverter
            .GetBytes((ushort)1)
            .CopyTo(_memory.Span.Slice(_consumed, sizeof(ushort)));

        _consumed += sizeof(ushort);
        _consumed += 4;

        BitConverter
            .GetBytes((ulong)size)
            .CopyTo(_memory.Span.Slice(_consumed, sizeof(ulong)));

        _consumed += sizeof(ulong);

        var globalHeapId = new GlobalHeapId(
            Address: _baseAddress,
            Index: _index
        );

        // object data
        var data = _memory.Slice(_consumed, size);

        _consumed += size;

        return (
            globalHeapId, 
            data
        );
    }

    private void AddNewCollection()
    {
        // TODO make encoding and decoding of collection more symmetrical

        var collection = new GlobalHeapCollection(default!)
        {
            Version = 1,
            CollectionSize = COLLECTION_SIZE
        };

        _baseAddress = _freeSpaceManager.Allocate(COLLECTION_SIZE);
        _memory = new byte[COLLECTION_SIZE - COLLECTION_HEADER_SIZE];

        //
        _consumed = 0;
        _index = 0;
        _collection = collection;
        _collectionMap[_baseAddress] = (collection, _memory);
    }

    public void Encode(BinaryWriter driver)
    {
        foreach (var entry in _collectionMap)
        {
            var address = entry.Key;
            var (collection, memory) = entry.Value;

            driver.BaseStream.Seek((long)address, SeekOrigin.Begin);

            // signature
            driver.Write(GlobalHeapCollection.Signature);

            // version
            driver.Write(collection.Version);

            // reserved
            driver.Seek(3, SeekOrigin.Current);

            // collection size
            driver.Write(collection.CollectionSize);

            // collection

#if NETSTANDARD2_0
            driver.Write(memory.Span.ToArray());
#else
            driver.Write(memory.Span);
#endif
        }
    }
}