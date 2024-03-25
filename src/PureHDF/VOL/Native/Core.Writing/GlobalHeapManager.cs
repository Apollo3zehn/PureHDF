using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal class GlobalHeapManager
{
    private const int ALIGNMENT = 8;
    private const int ABSOLUTE_MINIMUM_COLLECTION_SIZE = 4096; /* according to spec, includes collection header */
    private const int COLLECTION_HEADER_SIZE = 16;
    private const int OBJECT_HEADER_SIZE = 16;

    private readonly FreeSpaceManager _freeSpaceManager;
    private readonly Dictionary<long, GlobalHeapCollectionState> _collectionMap = new();
    private readonly H5DriverBase _driver;
    private readonly H5WriteOptions _options;

    private GlobalHeapCollectionState? _collectionState;
    private long _baseAddress;
    private ushort _index;
    private Memory<byte> _memory;

    public GlobalHeapManager(H5WriteOptions options, FreeSpaceManager freeSpaceManager, H5DriverBase driver)
    {
        if (options.MinimumGlobalHeapCollectionSize < ABSOLUTE_MINIMUM_COLLECTION_SIZE)
            throw new Exception($"The absolute minimum global heap collection size is {ABSOLUTE_MINIMUM_COLLECTION_SIZE} bytes");

        _options = options;
        _freeSpaceManager = freeSpaceManager;
        _driver = driver;
    }

    public (WritingGlobalHeapId, Memory<byte>) AddObject(int objectSize)
    {
        // validation
        var collectionState = _collectionState;

        if (collectionState is null ||
            collectionState.Consumed + OBJECT_HEADER_SIZE + AlignSize(objectSize) > collectionState.Memory.Length)
        {
            collectionState = AddNewCollection(
                collectionSize: Math.Max(
                    _options.MinimumGlobalHeapCollectionSize, 
                    AlignSize(objectSize) + OBJECT_HEADER_SIZE + COLLECTION_HEADER_SIZE));
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
            .GetBytes((ulong)objectSize)
            .CopyTo(_memory.Span.Slice(collectionState.Consumed, sizeof(ulong)));

        collectionState.Consumed += sizeof(ulong);

        var globalHeapId = new WritingGlobalHeapId(
            Address: (ulong)_baseAddress,
            Index: _index
        );

        // object data
        var data = _memory.Slice(collectionState.Consumed, objectSize);

        var alignedSize = AlignSize(objectSize);
        collectionState.Consumed += alignedSize;

        return (
            globalHeapId,
            data
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AlignSize(int objectSize)
    {
        /* H5HGpkg.h #define H5HG_ALIGN(X) */
        return ALIGNMENT * ((objectSize + ALIGNMENT - 1) / ALIGNMENT);
    }

    private GlobalHeapCollectionState AddNewCollection(int collectionSize)
    {
        // flush before we are able to continue
        if (
            _collectionMap.Sum(entry => (long)entry.Value.Collection.CollectionSize) >= 
            _options.GlobalHeapFlushThreshold
        )
        {
            Encode();
            _collectionMap.Clear();
        }

        // TODO make encoding and decoding of collection more symmetrical

        var collection = new GlobalHeapCollection(default!)
        {
            Version = 1,
            CollectionSize = (ulong)collectionSize
        };

        _baseAddress = _freeSpaceManager.Allocate(collectionSize);
        _memory = new byte[collectionSize - COLLECTION_HEADER_SIZE];

        //
        _index = 0;

        var collectionState = new GlobalHeapCollectionState(
            Collection: collection,
            Memory: _memory);

        _collectionState = collectionState;
        _collectionMap[_baseAddress] = collectionState;

        return collectionState;
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