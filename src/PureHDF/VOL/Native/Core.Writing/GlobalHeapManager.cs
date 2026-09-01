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
    private readonly Dictionary<AllocationKind, GlobalHeapCollectionState> _openCollections = new();
    private readonly H5DriverBase _driver;
    private readonly H5WriteOptions _options;

    public GlobalHeapManager(H5WriteOptions options, FreeSpaceManager freeSpaceManager, H5DriverBase driver)
    {
        if (options.MinimumGlobalHeapCollectionSize < ABSOLUTE_MINIMUM_COLLECTION_SIZE)
            throw new Exception($"The absolute minimum global heap collection size is {ABSOLUTE_MINIMUM_COLLECTION_SIZE} bytes");

        _options = options;
        _freeSpaceManager = freeSpaceManager;
        _driver = driver;
    }

    /// <summary>
    /// What the collections opened from here hold, and therefore where they are allocated.
    /// </summary>
    /// <remarks>
    /// A global heap collection is metadata when it holds attribute values, which a viewer reads while
    /// browsing a file, and raw data when it holds a dataset's own variable-length elements, which it
    /// reads only when reading that dataset. Both go through this one manager, so without the
    /// distinction a dataset of variable-length strings has its entire payload allocated as structure -
    /// measured at 97% of such a file - and a placement that segregates structure from payload has
    /// nothing left to segregate.
    /// <para>
    /// Set around an encode rather than passed in, because the delegates that call
    /// <see cref="AddObject" /> are built by <c>DatatypeMessage.Create</c>, which is shared by both
    /// paths and cannot tell them apart. The writer scopes it at the two points where it can: a
    /// dataset's data write, and an attribute's encode. Both restore it, so nesting - an object
    /// reference inside dataset data encodes the group it points at, attributes and all - stays
    /// correctly attributed.
    /// </para>
    /// <para>
    /// A collection is kept open per kind, so the two never share one. That is what makes the
    /// classification meaningful: a collection is allocated whole, so one shared collection would put
    /// both kinds at whichever address the first object claimed.
    /// </para>
    /// </remarks>
    public AllocationKind AllocationKind { get; set; } = AllocationKind.Metadata;

    public (WritingGlobalHeapId, Memory<byte>) AddObject(int objectSize)
    {
        // validation
        var kind = AllocationKind;

        _openCollections.TryGetValue(kind, out var collectionState);

        if (collectionState is null ||
            collectionState.Consumed + OBJECT_HEADER_SIZE + AlignSize(objectSize) > collectionState.Memory.Length)
        {
            collectionState = AddNewCollection(
                kind,
                collectionSize: Math.Max(
                    _options.MinimumGlobalHeapCollectionSize, 
                    AlignSize(objectSize) + OBJECT_HEADER_SIZE + COLLECTION_HEADER_SIZE));
        }

        var memory = collectionState.Memory;

        // encode object header
        collectionState.Index++;

        BitConverter
            .GetBytes(collectionState.Index)
            .CopyTo(memory.Span.Slice(collectionState.Consumed, sizeof(ushort)));

        collectionState.Consumed += sizeof(ushort);

        BitConverter
            .GetBytes((ushort)1)
            .CopyTo(memory.Span.Slice(collectionState.Consumed, sizeof(ushort)));

        collectionState.Consumed += sizeof(ushort);
        collectionState.Consumed += 4;

        BitConverter
            .GetBytes((ulong)objectSize)
            .CopyTo(memory.Span.Slice(collectionState.Consumed, sizeof(ulong)));

        collectionState.Consumed += sizeof(ulong);

        var globalHeapId = new WritingGlobalHeapId(
            Address: (ulong)collectionState.BaseAddress,
            Index: collectionState.Index
        );

        // object data
        var data = memory.Slice(collectionState.Consumed, objectSize);

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

    private GlobalHeapCollectionState AddNewCollection(AllocationKind kind, int collectionSize)
    {
        // flush before we are able to continue
        if (
            _collectionMap.Sum(entry => (long)entry.Value.Collection.CollectionSize) >= 
            _options.GlobalHeapFlushThreshold
        )
        {
            Encode();
            _collectionMap.Clear();

            // Every open collection has just been written and dropped from the map, so none of them may
            // take another object - anything added to one afterwards would never be encoded again.
            _openCollections.Clear();
        }

        // TODO make encoding and decoding of collection more symmetrical

        var collection = new GlobalHeapCollection(default!)
        {
            Version = 1,
            CollectionSize = (ulong)collectionSize
        };

        var baseAddress = _freeSpaceManager.Allocate(collectionSize, kind);

        var collectionState = new GlobalHeapCollectionState(
            Collection: collection,
            Memory: new byte[collectionSize - COLLECTION_HEADER_SIZE],
            BaseAddress: baseAddress);

        _openCollections[kind] = collectionState;
        _collectionMap[baseAddress] = collectionState;

        return collectionState;
    }

    public void Encode()
    {
        var driver = _driver;

        // capture current position
        var position = driver.Position;

        foreach (var entry in _collectionMap)
        {
            var address = entry.Key;
            var (collection, memory, _) = entry.Value;
            var consumed = entry.Value.Consumed;
            var remainingSpace = (ulong)(memory.Length - consumed);

            driver.SeekRelativeToBaseAddress(address);

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
            if (remainingSpace >= OBJECT_HEADER_SIZE)
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

        // restore original position
        driver.Seek(position, SeekOrigin.Begin);
    }
}