using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal class GlobalHeapManager
{
    private const int ALIGNMENT = 8;
    private const int ABSOLUTE_MINIMUM_COLLECTION_SIZE = 4096; /* according to spec, includes collection header */
    private const int COLLECTION_HEADER_SIZE = 16;
    private const int OBJECT_HEADER_SIZE = 16;

    // An object's index within its collection is 16 bits wide in the file format, whatever room the
    // heap ID has for it.
    private const int MAXIMUM_OBJECTS_PER_COLLECTION = ushort.MaxValue;

    // A reader loads a collection whole to reach any one object in it, so a heap planned for a write is
    // split into adjacent collections of at most this size: reading one value never costs more than this,
    // while the write's values stay together in the file.
    private const int MAXIMUM_PLANNED_COLLECTION_SIZE = 1 << 20;

    private readonly FreeSpaceManager _freeSpaceManager;

    // Allocated but not yet written, in allocation order.
    private readonly List<GlobalHeapCollectionState> _pendingCollections = new();
    private readonly Dictionary<AllocationKind, GlobalHeapCollectionState> _openCollections = new();

    // Set while a dataset write is measured (see BeginRecording) and while it is written into the collections
    // planned from that measurement (see TryBeginPlanned).
    private List<int>? _recordedSizes;
    private List<GlobalHeapCollectionState>? _plannedCollections;
    private int _plannedIndex;
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
    /// browsing a file, or a compact dataset's variable-length values, whose data is part of its header;
    /// it is raw data when it holds any other dataset's own variable-length elements, which a viewer
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
        var kind = AllocationKind;

        // A dataset's own values, while one of its writes is being measured or written to plan. Anything
        // else arriving meanwhile - an attribute of an object a reference points at - is metadata, and goes
        // where metadata always goes.
        if (kind == AllocationKind.RawData)
        {
            if (_recordedSizes is not null)
            {
                _recordedSizes.Add(objectSize);
                return (default, new byte[objectSize]);
            }

            if (_plannedCollections is not null)
                return AddObjectTo(NextPlannedCollection(objectSize), objectSize);
        }

        _openCollections.TryGetValue(kind, out var collectionState);

        if (collectionState is not null && Fits(collectionState, objectSize))
            return AddObjectTo(collectionState, objectSize);

        var requiredSize = AlignSize(objectSize) + OBJECT_HEADER_SIZE + COLLECTION_HEADER_SIZE;

        // Too large for a collection of the usual size: it gets one of its own, and the open collection stays
        // open. Replacing the open collection instead would abandon whatever room it has left, so that one
        // large value among small ones would cost a nearly empty collection.
        if (requiredSize > _options.MinimumGlobalHeapCollectionSize)
            return AddObjectTo(AddNewCollection(kind, requiredSize), objectSize);

        collectionState = AddNewCollection(kind, _options.MinimumGlobalHeapCollectionSize);
        _openCollections[kind] = collectionState;

        return AddObjectTo(collectionState, objectSize);
    }

    /// <summary>
    /// Starts measuring a dataset write: until <see cref="EndRecording" />, its objects are only counted and
    /// sized, and are given memory that is thrown away.
    /// </summary>
    public void BeginRecording()
    {
        _recordedSizes = new List<int>();
    }

    /// <summary>
    /// Ends a measurement started by <see cref="BeginRecording" />.
    /// </summary>
    /// <returns>The size of every object the write added, in the order it added them.</returns>
    public List<int> EndRecording()
    {
        var recordedSizes = _recordedSizes ?? throw new InvalidOperationException("No recording is in progress.");
        _recordedSizes = null;

        return recordedSizes;
    }

    /// <summary>
    /// Allocates collections sized exactly for the objects <paramref name="objectSizes" /> describes, and serves
    /// a dataset write's objects from them until <see cref="EndPlanned" />.
    /// </summary>
    /// <remarks>
    /// Gives a write's variable-length values one contiguous heap of its own, rather than a run of
    /// minimum-sized collections shared with whatever else needs heap space at the time. Collections are
    /// split where one would exceed the object limit or 1 MiB.
    /// <para>
    /// Declined for a write whose values would not fill a collection of the minimum size, which every
    /// collection has to be: one per such write would mostly be padding, so small writes keep sharing.
    /// </para>
    /// </remarks>
    /// <returns>False if the write should share collections instead.</returns>
    public bool TryBeginPlanned(List<int> objectSizes)
    {
        var minimumSize = _options.MinimumGlobalHeapCollectionSize;
        var totalSize = COLLECTION_HEADER_SIZE + OBJECT_HEADER_SIZE + objectSizes.Sum(size => (long)OBJECT_HEADER_SIZE + AlignSize(size));

        if (objectSizes.Count == 0 || totalSize < minimumSize)
            return false;

        var planned = new List<GlobalHeapCollectionState>();
        var objectCount = 0;
        var objectBytes = 0L;

        void Close()
        {
            // Room for the header of object 0 is kept, which records the free space - none, here, unless the
            // collection had to be padded to the minimum size.
            var collectionSize = (int)Math.Max(minimumSize, COLLECTION_HEADER_SIZE + objectBytes + OBJECT_HEADER_SIZE);
            planned.Add(CreateCollection(collectionSize, AllocationKind.RawData));

            objectCount = 0;
            objectBytes = 0;
        }

        foreach (var size in objectSizes)
        {
            var objectSize = OBJECT_HEADER_SIZE + AlignSize(size);

            if (objectCount == MAXIMUM_OBJECTS_PER_COLLECTION ||
                objectCount > 0 && COLLECTION_HEADER_SIZE + objectBytes + objectSize + OBJECT_HEADER_SIZE > MAXIMUM_PLANNED_COLLECTION_SIZE)
            {
                Close();
            }

            objectCount++;
            objectBytes += objectSize;
        }

        Close();

        _plannedCollections = planned;
        _plannedIndex = 0;

        return true;
    }

    /// <summary>
    /// Writes the collections <see cref="TryBeginPlanned" /> allocated, now that the write has filled them.
    /// </summary>
    public void EndPlanned()
    {
        var planned = _plannedCollections ?? throw new InvalidOperationException("No planned write is in progress.");
        _plannedCollections = null;

        WriteCollections(planned);
    }

    /// <summary>
    /// Ends a planned write that failed, without writing its collections.
    /// </summary>
    public void AbandonPlanned()
    {
        _plannedCollections = null;
    }

    private GlobalHeapCollectionState NextPlannedCollection(int objectSize)
    {
        var planned = _plannedCollections!;

        while (_plannedIndex < planned.Count && !Fits(planned[_plannedIndex], objectSize))
            _plannedIndex++;

        return _plannedIndex < planned.Count
            ? planned[_plannedIndex]
            : throw new InvalidOperationException(
                "A write added heap objects its measurement did not account for. Encoding it twice must add the same objects in the same order.");
    }

    private bool Fits(GlobalHeapCollectionState collectionState, int objectSize)
    {
        return collectionState.Index < MAXIMUM_OBJECTS_PER_COLLECTION &&
            collectionState.Consumed + OBJECT_HEADER_SIZE + AlignSize(objectSize) <= collectionState.Memory.Length;
    }

    private (WritingGlobalHeapId, Memory<byte>) AddObjectTo(GlobalHeapCollectionState collectionState, int objectSize)
    {
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

    /// <summary>
    /// What an object of <paramref name="objectSize" /> bytes takes up in a collection: its header and its
    /// data, aligned.
    /// </summary>
    public static int GetStoredSize(int objectSize)
        => OBJECT_HEADER_SIZE + AlignSize(objectSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AlignSize(int objectSize)
    {
        /* H5HGpkg.h #define H5HG_ALIGN(X) */
        return ALIGNMENT * ((objectSize + ALIGNMENT - 1) / ALIGNMENT);
    }

    private GlobalHeapCollectionState AddNewCollection(AllocationKind kind, int collectionSize)
    {
        // A collection is always allocated now and written later - filled progressively, flushed in
        // batches - so a payload collection is clustered rather than bump-allocated. See
        // AllocationKind.DeferredRawData: the gap between allocating and writing is what pins a
        // streaming writer's buffers, and scattering these holes through the file pins all of them.
        var collectionState = CreateCollection(
            collectionSize,
            kind == AllocationKind.Metadata ? AllocationKind.Metadata : AllocationKind.DeferredRawData);

        _pendingCollections.Add(collectionState);

        return collectionState;
    }

    private GlobalHeapCollectionState CreateCollection(int collectionSize, AllocationKind allocationKind)
    {
        // TODO make encoding and decoding of collection more symmetrical

        var collection = new GlobalHeapCollection(default!)
        {
            Version = 1,
            CollectionSize = (ulong)collectionSize
        };

        var baseAddress = _freeSpaceManager.Allocate(collectionSize, allocationKind);

        return new GlobalHeapCollectionState(
            Collection: collection,
            Memory: new byte[collectionSize - COLLECTION_HEADER_SIZE],
            BaseAddress: baseAddress);
    }

    /// <summary>
    /// Writes every collection that can take no more objects, leaving only the ones still being filled - one
    /// per allocation kind at most - allocated but unwritten.
    /// </summary>
    /// <remarks>
    /// Only to be called between top-level encodes. A collection is replaced while an object is being added,
    /// which can be in the middle of a nested variable-length value whose outer object it holds and which is
    /// still being filled - so it is written at the next point where nothing is half-encoded. Written and
    /// dropped as soon as it is replaced, it would miss the rest of that value, which would then never
    /// reach the file.
    /// <para>
    /// Each unwritten collection is a hole behind the end of what has been written, and a destination that
    /// only accepts complete parts - an upload in parts - has to hold the part around it until the hole is
    /// filled. Writing them here bounds that to the collections still filling.
    /// </para>
    /// </remarks>
    public void WriteClosedCollections()
    {
        var open = _openCollections.Values;
        var closed = _pendingCollections.Where(state => !open.Contains(state)).ToList();

        if (closed.Count == 0)
            return;

        WriteCollections(closed);
        _pendingCollections.RemoveAll(closed.Contains);
    }

    /// <summary>
    /// Writes every collection allocated so far. Like <see cref="WriteClosedCollections" />, only to be called
    /// between top-level encodes.
    /// </summary>
    public void Encode()
    {
        WriteCollections(_pendingCollections);

        _pendingCollections.Clear();

        // Every open collection has just been written and dropped, so none of them may take another object -
        // anything added to one afterwards would never be written.
        _openCollections.Clear();
    }

    private void WriteCollections(List<GlobalHeapCollectionState> collections)
    {
        var driver = _driver;

        // capture current position
        var position = driver.Position;

        foreach (var collectionState in collections)
        {
            var address = collectionState.BaseAddress;
            var (collection, memory, _) = collectionState;
            var consumed = collectionState.Consumed;
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
