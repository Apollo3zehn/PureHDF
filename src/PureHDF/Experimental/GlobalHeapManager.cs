namespace PureHDF.Experimental;

internal class GlobalHeapManager
{
    private const int ALIGNMENT = 8;
    private const ulong SPEC_COLLECTION_SIZE = 4096; /* according to spec, includes collection header */
    private const ulong COLLECTION_SIZE = SPEC_COLLECTION_SIZE - COLLECTION_HEADER_SIZE; /* without collection header */
    private const int COLLECTION_HEADER_SIZE = 16;
    private const int OBJECT_HEADER_SIZE = 16;

    private readonly FreeSpaceManager _freeSpaceManager;
    private readonly Dictionary<ulong, GlobalHeapCollectionState> _collectionMap = new();
    private GlobalHeapCollectionState? _collectionState;
    private ulong _baseAddress;
    private ushort _index;
        private Memory<byte> _memory;

    public GlobalHeapManager(FreeSpaceManager freeSpaceManager)
    {
        _freeSpaceManager = freeSpaceManager;
    }

    public (GlobalHeapId, Memory<byte>) AddObject(int size)
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

        var globalHeapId = new GlobalHeapId(
            Address: _baseAddress,
            Index: _index
        );

        // object data
        var data = _memory.Slice(collectionState.Consumed, size);

        /* H5HGpkg.h #define H5HG_ALIGN(X) */
        var alignedSize = ALIGNMENT * ((size + ALIGNMENT - 1) / ALIGNMENT);
        collectionState.Consumed += alignedSize;

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
            CollectionSize = SPEC_COLLECTION_SIZE
        };

        _baseAddress = _freeSpaceManager.Allocate(COLLECTION_HEADER_SIZE + COLLECTION_SIZE);
        _memory = new byte[COLLECTION_SIZE];

        //
        _index = 0;

        var collectionState = new GlobalHeapCollectionState(
            Collection: collection,
            Memory: _memory);

        _collectionState = collectionState;
        _collectionMap[_baseAddress] = collectionState;
    }

    public void Encode(BinaryWriter driver)
    {
        foreach (var entry in _collectionMap)
        {
            var address = entry.Key;
            var (collection, memory) = entry.Value;
            var consumed = entry.Value.Consumed;
            var remainingSpace = (ulong)(memory.Length - consumed);

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
            driver.Write(memory.Span[..consumed].ToArray());
#else
            driver.Write(memory.Span[..consumed]);
#endif

            // Global Heap Object 0
            if (remainingSpace > OBJECT_HEADER_SIZE)
            {
                /* The field Object Size for Object 0 indicates the amount of possible free space 
                   in the collection INCLUDING the 16-byte header size of Object 0.  */
                driver.Seek(sizeof(ushort) + sizeof(ushort) + 4, SeekOrigin.Current);
                driver.Write(remainingSpace);
                remainingSpace -= OBJECT_HEADER_SIZE;
            }

            var endAddress = driver.BaseStream.Position + (long)remainingSpace;

            if (driver.BaseStream.Length < endAddress)
                driver.BaseStream.SetLength(endAddress);
        }
    }
}