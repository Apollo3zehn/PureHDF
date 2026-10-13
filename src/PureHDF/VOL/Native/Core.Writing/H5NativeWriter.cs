using PureHDF.Selections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF;

partial class H5NativeWriter
{
    private delegate ulong EncodeDatasetDispatch(H5NativeWriter writer, H5Dataset dataset);

    private delegate void WriteDatasetDispatch<T>(
        H5D_Base h5d, object encode, T data, Selection? memorySelection, Selection? fileSelection);

    // One closed generic method per dataset type, built once. Building it per call (MakeGenericMethod
    // plus Invoke) would allocate a method, its type arguments and an argument array for every dataset
    // encoded and every write - the bulk of what encoding a file of many small datasets allocates.
    private static readonly ConcurrentDictionary<Type, EncodeDatasetDispatch> _encodeDatasetDispatchCache = new();

    private ulong _rootGroupAddress;

    // The most data a dataset given with the tree may hold and still be left out of the filters set for
    // every dataset (H5WriteOptions.Filters). Measured: up to 64 bytes, compact beat deflate on file size,
    // write and read time; at 256 bytes, deflate was 42% smaller.
    private const int MAXIMUM_UNFILTERED_DATASET_SIZE = 64;

    // Shared rather than created per dataset: it is immutable and the same for all of them, and a
    // dataset whose DatasetInfo the writer keeps until Dispose would otherwise hold a copy of its own.
    /* "The default fill value is 0 (zero), ..." (https://docs.hdfgroup.org/hdf5/develop/group___d_c_p_l.html) */
    internal static readonly FillValueMessage DefaultFillValueMessage = new(
        AllocationTime: SpaceAllocationTime.Early,
        FillTime: FillValueWriteTime.Never,
        Value: default
    )
    {
        Version = 3
    };

    internal H5NativeWriter(H5File file, Stream stream, H5WriteOptions options, bool leaveOpen)
    {
        // Not required to be readable: checksums are computed from the encoded bytes, so nothing written
        // is read back, and a destination that cannot serve reads (an upload) can be written to.
        if (!stream.CanWrite || !stream.CanSeek)
            throw new Exception("The stream must be writable and seekable.");

        // The Stream constructor takes cursor mode by construction, which is what the write path
        // needs: it writes at the wrapped stream's own cursor, and positionless mode would decouple
        // that cursor (Seek moves _position, Write lands at Stream.Position) and write at the wrong
        // offsets. IConcurrentStream is a read-side interface; this keeps a stream that happens to
        // implement it writing at the wrapped stream's own cursor, unaffected by the read-side
        // interface.
        //
        // Front loading without a given reservation encodes the structure once and places it when it is
        // complete; until then it is held in memory. See StructureStaging.
        var stagesStructure =
            options.MetadataPlacement == H5MetadataPlacement.FrontLoaded &&
            options.MetadataReservation == 0;

        var stagingStream = stagesStructure
            ? new StructureStagingStream(stream, leaveOpen)
            : null;

        var driver = new H5StreamDriver(stagingStream ?? stream, leaveOpen: leaveOpen);

        if (options.UserBlockSize != 0)
        {
            /* https://stackoverflow.com/a/600306/1636629 */
            var isPowerOfTwo = (options.UserBlockSize & (options.UserBlockSize - 1)) == 0;

            if (isPowerOfTwo && options.UserBlockSize >= 512)
                driver.SetBaseAddress(options.UserBlockSize);

            else
                throw new Exception("The user block size is invalid.");
        }

        if (options.MaximumCompactDatasetSize < 0)
            throw new Exception("The maximum compact dataset size must not be negative.");

        if (options.MetadataReservation < 0)
            throw new Exception("The metadata reservation must not be negative.");

        // Only meaningful away from Interleaved, which passes zero deliberately below. Rejected rather
        // than tolerated because a non-positive ceiling silently turns Aggregated into Interleaved: the
        // block path is gated on a positive size, so the caller asks for clustering and gets none, with
        // nothing to indicate why.
        if (options.MetadataPlacement != H5MetadataPlacement.Interleaved && options.MetadataBlockSize <= 0)
            throw new Exception("The metadata block size must be greater than zero.");

        var freeSpaceManager = new FreeSpaceManager(
            options.MetadataPlacement,
            blockSize: options.MetadataPlacement == H5MetadataPlacement.Interleaved
                ? 0
                : options.MetadataBlockSize);

        // The superblock must sit at offset zero, which is why it does not go through Allocate - see
        // AllocateAtFront.
        freeSpaceManager.AllocateAtFront(Superblock23.ENCODE_SIZE);

        if (stagesStructure)
        {
            freeSpaceManager.ReserveProvisionalRegion();
        }

        else if (options.MetadataPlacement == H5MetadataPlacement.FrontLoaded)
        {
            freeSpaceManager.ReserveMetadataRegion(options.MetadataReservation);
        }

        var globalHeapManager = new GlobalHeapManager(options, freeSpaceManager, driver);

        var writeContext = new NativeWriteContext(
            Writer: this,
            File: file,
            Driver: driver,
            FreeSpaceManager: freeSpaceManager,
            GlobalHeapManager: globalHeapManager,
            WriteOptions: options,
            Objects: new(),
            DatasetInfoToObjectHeaderMap: new(),
            TypeToMessageMap: new(),
            ObjectReferenceCountMap: new(),
            RawValueToDatasetMap: new(ReferenceEqualityComparer.Instance),
            ShortlivedStream: new(memory: default)
        )
        {
            Staging = stagingStream is null ? null : new StructureStaging(stagingStream)
        };

        File = file;
        Context = writeContext;
    }

    internal NativeWriteContext Context { get; }

    internal void Write()
    {
        // Count the incoming links for every shared object so that multiply-linked
        // objects can carry an accurate object reference count message (hard links).
        var objectCount = CountReferences(File, Context.ObjectReferenceCountMap);

        // Sized up front because it ends up with an entry per object and lives until Dispose. Grown by
        // doubling instead, it ends up to twice the size it needs, and every step on the way leaves the
        // previous entry array behind on the large object heap.
        Context.Objects.EnsureCapacity(objectCount);

        // root group
        Context.Driver.SeekRelativeToBaseAddress(Superblock23.ENCODE_SIZE);
        _rootGroupAddress = EncodeGroup(File);

        PlaceStructure();
        WriteAbandoned();
    }

    /// <summary>
    /// Fills the tails of the blocks replaced so far with zeros. See <see cref="FreeSpaceManager.TakeAbandoned" />.
    /// </summary>
    internal void WriteAbandoned()
    {
        var abandoned = Context.FreeSpaceManager.TakeAbandoned();

        if (abandoned.Count == 0)
            return;

        var driver = Context.Driver;
        var position = driver.Position;
        Span<byte> zeros = stackalloc byte[4096];

        foreach (var (address, length) in abandoned)
        {
            driver.SeekRelativeToBaseAddress(address);

            for (var remaining = length; remaining > 0; remaining -= zeros.Length)
                driver.Write(zeros[..(int)Math.Min(remaining, zeros.Length)]);
        }

        driver.Seek(position, SeekOrigin.Begin);
    }

    /// <summary>
    /// Finishes a write that holds its structure until it is complete: moves the payload behind the
    /// structure, corrects the payload addresses the structure names, writes the structure, and then the
    /// payload that came with the tree.
    /// </summary>
    /// <remarks>
    /// Does nothing for a write that does not stage its structure, or once it has run.
    /// </remarks>
    private void PlaceStructure()
    {
        if (Context.Staging is not { } staging)
            return;

        Context.Staging = null;

        var freeSpaceManager = Context.FreeSpaceManager;

        // Attribute values are structure, and the last of it.
        Context.GlobalHeapManager.Encode();

        freeSpaceManager.PlaceProvisionalRegion();

        var buffer = default(byte[]);

        try
        {
            foreach (var provisional in staging.Addresses)
            {
                if (buffer is null || buffer.Length < provisional.HeaderLength)
                {
                    if (buffer is not null)
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);

                    buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(provisional.HeaderLength);
                }

                var header = buffer.AsSpan(0, provisional.HeaderLength);
                var field = header.Slice(provisional.AddressOffset, sizeof(ulong));

                staging.Stream.ReadStaged(provisional.HeaderPosition, header);

                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
                    field,
                    freeSpaceManager.Relocate(System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(field)));

                ChecksummedStructure.UpdateChecksum(header);
                staging.Stream.Patch(provisional.HeaderPosition, header);
            }
        }

        finally
        {
            if (buffer is not null)
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        staging.Stream.Commit();

        // Payload written later finds its address in the layout, so the layout has to name where the
        // payload ended up rather than where it was placed provisionally.
        foreach (var entry in Context.Objects.Values)
            entry.Deferred?.Relocate(freeSpaceManager);

        foreach (var write in staging.PostponedWrites)
            write();
    }

    /// <remarks>
    /// Only objects linked more than once end up in <paramref name="counts" />, since a missing entry
    /// already means a count of one. A count for every object would be one dictionary entry per
    /// dataset, held for the writer's whole lifetime - tens of megabytes for a few hundred thousand
    /// datasets - to record a number that is almost always one. The set of objects seen so far is
    /// needed only for the walk and is dropped with it.
    /// </remarks>
    /// <returns>How many distinct objects are linked below <paramref name="root" />.</returns>
    private int CountReferences(H5Group root, Dictionary<H5Object, int> counts)
    {
        var seen = new HashSet<H5Object>();

        void Walk(H5Group group)
        {
            foreach (var entry in group)
            {
                // Soft links do not contribute to the target's hard-link count.
                if (entry.Value is H5SoftLink)
                    continue;

                // Resolve to the (possibly cached) H5Object so that both actual H5Object
                // instances and shared raw values are counted by object identity.
                var h5Object = GetH5Object(entry.Value);

                if (!seen.Add(h5Object))
                {
                    counts[h5Object] = counts.GetValueOrDefault(h5Object, 1) + 1;
                    continue;
                }

                if (h5Object is H5Group childGroup)
                    Walk(childGroup);
            }
        }

        Walk(root);

        return seen.Count;
    }

    private H5Object GetH5Object(object value)
    {
        if (value is H5Object h5Object)
            return h5Object;

        // Wrap raw values into a single H5Dataset per instance so that a value assigned
        // to multiple locations is deduplicated and hard-linked just like an H5Object.
        if (!Context.RawValueToDatasetMap.TryGetValue(value, out var dataset))
        {
            dataset = new H5Dataset(value);
            Context.RawValueToDatasetMap[value] = dataset;
        }

        return dataset;
    }

    private void AppendReferenceCountToHeaderMessages(H5Object h5Object, List<HeaderMessage> headerMessages)
    {
        // The object reference count message is only required for multiply-linked
        // objects; a missing message is interpreted as a reference count of 1.
        if (Context.ObjectReferenceCountMap.TryGetValue(h5Object, out var count) && count > 1)
        {
            var objectReferenceCountMessage = new ObjectReferenceCountMessage(
                ReferenceCount: (uint)count
            )
            {
                Version = 0
            };

            headerMessages.Add(ToHeaderMessage(objectReferenceCountMessage));
        }
    }

    /// <summary>
    /// Returns the address of <paramref name="h5Object" />'s object header, encoding the object first if
    /// this is the first link to it.
    /// </summary>
    internal ulong GetOrEncodeObject(H5Object h5Object)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(Context.Objects, h5Object, out var exists);

        if (exists)
        {
            return entry.Address == default
                ? throw new Exception("The current object is already being encoded which suggests a circular reference.")
                : entry.Address;
        }

        var address = h5Object switch
        {
            H5Group group => EncodeGroup(group),
            H5Dataset dataset => EncodeDataset(dataset),
            _ => throw new Exception("Named data types cannot yet be used in combination with H5ObjectReference.")
        };

        // Looked up again rather than through the ref above: encoding adds entries for everything
        // below this object, which can grow the map and move its entries.
        CollectionsMarshal.GetValueRefOrNullRef(Context.Objects, h5Object).Address = address;

        return address;
    }

    internal ulong EncodeGroup(
        H5Group group)
    {
        var headerMessages = new List<HeaderMessage>();

        // link info message
        var linkInfoMessage = new LinkInfoMessage(
            Flags: CreationOrderFlags.None,
            MaximumCreationIndex: default,
            FractalHeapAddress: Superblock.UndefinedAddress,
            BTree2NameIndexAddress: Superblock.UndefinedAddress,
            BTree2CreationOrderIndexAddress: Superblock.UndefinedAddress
        )
        {
            Version = 0
        };

        headerMessages.Add(ToHeaderMessage(linkInfoMessage));

        // group info message
        //
        // Required, not optional: the HDF5 C library reads this message in H5G_obj_insert() to learn the
        // link phase change thresholds before it inserts a link. Without it, adding ANY named object to
        // a group - a dataset as much as a group - fails with "message type not found", so a file this
        // writer produced could be read by every tool but extended by none of them.
        //
        // No flags, which means the reader applies its own defaults for those thresholds (8 compact /
        // 6 dense in the C library). Declaring values here would state a policy this writer does not
        // implement, since it always stores links compactly.
        //
        // See https://github.com/HDFGroup/hdf5/issues/6616 for more info.
        var groupInfoMessage = new GroupInfoMessage(
            Flags: default,
            MaximumCompactValue: default,
            MinimumDenseValue: default,
            EstimatedEntryCount: default,
            EstimatedEntryLinkNameLength: default
        )
        {
            Version = 0
        };

        headerMessages.Add(ToHeaderMessage(groupInfoMessage));

        if (group.InternalAttributes is not null)
            AppendAttributesToHeaderMessages(group.InternalAttributes, headerMessages, Context);

        foreach (var entry in group)
        {
            ValidateLinkName(entry.Key);

            LinkType linkType;
            LinkInfo linkInfo;

            if (entry.Value is H5SoftLink softLink)
            {
                linkType = LinkType.Soft;
                linkInfo = new SoftLinkInfo(softLink.Target);
            }

            else
            {
                var linkAddress = GetOrEncodeObject(GetH5Object(entry.Value));

                linkType = LinkType.Hard;
                linkInfo = new HardLinkInfo(HeaderAddress: linkAddress);
            }

            var flags =
                LinkInfoFlags.LinkNameLengthSizeUpperBit |
                LinkInfoFlags.LinkNameEncodingFieldIsPresent;

            if (linkType == LinkType.Soft)
                flags |= LinkInfoFlags.LinkTypeFieldIsPresent;

            var linkMessage = new LinkMessage(
                Flags: flags,
                LinkType: linkType,
                CreationOrder: default,
                LinkName: entry.Key,
                LinkInfo: linkInfo
            )
            {
                Version = 1
            };

            headerMessages.Add(ToHeaderMessage(linkMessage));
        }

        AppendReferenceCountToHeaderMessages(group, headerMessages);

        var objectHeader = new ObjectHeader2(
            Address: default,
            Flags: ObjectHeaderFlags.SizeOfChunk1 | ObjectHeaderFlags.SizeOfChunk2,
            AccessTime: default,
            ModificationTime: default,
            ChangeTime: default,
            BirthTime: default,
            MaximumCompactAttributesCount: default,
            MinimumDenseAttributesCount: default,
            HeaderMessages: headerMessages
        )
        {
            Version = 2
        };

        // encode object header
        // ObjectHeader2.Encode() returns a ValueTask but completes synchronously. This method and its
        // public entry point (H5File.Write(), which has no async counterpart) are synchronous, so the
        // call is bridged here.
        var address = objectHeader.Encode(Context).GetAwaiter().GetResult();

        return address;
    }

    internal ulong EncodeDataset(
        H5Dataset dataset)
    {
        var dispatch = _encodeDatasetDispatchCache.GetOrAdd(dataset.Type, CreateEncodeDatasetDispatch);

        return dispatch(this, dataset);
    }

    private static EncodeDatasetDispatch CreateEncodeDatasetDispatch(Type type)
    {
        var (elementType, isScalar) = WriteUtils.GetElementType(type);

        var method = typeof(H5NativeWriter)
            .GetMethod(isScalar ? nameof(EncodeScalarDataset) : nameof(EncodeNonScalarDataset), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type, elementType);

        return method.CreateDelegate<EncodeDatasetDispatch>();
    }

    // Two entry points rather than a captured isScalar, so the cached delegate binds straight to a
    // static method with no closure in between.
    private static ulong EncodeScalarDataset<T, TElement>(H5NativeWriter writer, H5Dataset dataset)
        => writer.InternalEncodeDataset<T, TElement>(dataset, dataset.Data is null ? default! : (T)dataset.Data, isScalar: true);

    private static ulong EncodeNonScalarDataset<T, TElement>(H5NativeWriter writer, H5Dataset dataset)
        => writer.InternalEncodeDataset<T, TElement>(dataset, dataset.Data is null ? default! : (T)dataset.Data, isScalar: false);

    /// <summary>
    /// The write dispatch for <typeparamref name="T" />, built on first use. A static generic class
    /// rather than a dictionary, because <see cref="Write{T}" /> knows <typeparamref name="T" />
    /// statically and only the element type has to be found by reflection.
    /// </summary>
    private static class WriteDatasetDispatchCache<T>
    {
        public static readonly WriteDatasetDispatch<T> Dispatch =
            Create<WriteDatasetDispatch<T>>(nameof(WriteDatasetUntyped));

        public static readonly WriteWholeContiguousDispatch<T> WholeContiguous =
            Create<WriteWholeContiguousDispatch<T>>(nameof(WriteWholeContiguous));

        private static TDelegate Create<TDelegate>(string methodName) where TDelegate : Delegate
        {
            var (elementType, _) = WriteUtils.GetElementType(typeof(T));

            return typeof(H5NativeWriter)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(T), elementType)
                .CreateDelegate<TDelegate>();
        }
    }

    private static void WriteDatasetUntyped<T, TElement>(
        H5D_Base h5d, object encode, T data, Selection? memorySelection, Selection? fileSelection)
        => InternalWriteDataset(h5d, (EncodeDelegate<TElement>)encode, data, memorySelection, fileSelection);

    private delegate bool WriteWholeContiguousDispatch<T>(
        H5NativeWriter writer, DeferredContiguous target, T data);

    /// <summary>
    /// Writes all of a contiguous dataset's data in one go, when the data covers the dataset exactly.
    /// </summary>
    /// <remarks>
    /// The general path gets to the same single encoder call for this case, but on the way walks both
    /// selections and builds the layout, selections, encode info and closures that the walk needs -
    /// about 1.5 kB and most of the per-call cost of writing a small dataset.
    /// <para>
    /// Narrow on purpose: anything it does not recognise as the whole dataset written from one buffer
    /// returns false and is left to the general path, which also produces the errors for data that
    /// does not fit.
    /// </para>
    /// </remarks>
    /// <returns>True if the data was written; false if the general path has to write it.</returns>
    private static bool WriteWholeContiguous<T, TElement>(
        H5NativeWriter writer, DeferredContiguous target, T data)
    {
        // Opaque data is counted in bytes rather than elements, and a scalar dataspace has no
        // dimensions to compare the data against - both are left to the general path.
        // Variable-length values are left to it too, which measures them first and gives them a heap of
        // their own.
        if (target.Type.Class == DatatypeMessageClass.Opaque ||
            target.Type.IsReferenceOrContainsReferences() ||
            target.FileDims.Length == 0)
            return false;

        var (memoryData, _) = WriteUtils.ToMemory<T, TElement>(data);

        if (memoryData.Equals(default))
            return false;

        var elementCount = 1UL;

        foreach (var dimension in target.FileDims)
            elementCount *= dimension;

        if ((ulong)memoryData.Length != elementCount)
            return false;

        var driver = writer.Context.Driver;
        driver.SeekRelativeToBaseAddress((long)target.Address);

        ((EncodeDelegate<TElement>)target.Encode)(memoryData, new OffsetStream(driver));

        return true;
    }

    private ulong InternalEncodeDataset<T, TElement>(
        H5Dataset dataset,
        T data,
        bool isScalar)
    {
        Memory<TElement> memoryData;
        ulong[]? memoryDims;

        if (dataset.IsNullDataspace)
        {
            memoryData = default;
            memoryDims = default;
        }

        /* A scalar whose value is null is one element that is null, which is not the same thing as
         * carrying no data. WriteUtils.ToMemory cannot tell those apart - it is handed an object? and a
         * null reference is all it gets - so it answers with no dimensions, and no dimensions is how
         * DataspaceMessage.Create is asked for a null dataspace. This keeps nullable reference types
         * behaving like nullable value types.
         */
        else if (data is null && isScalar)
        {
            memoryData = new TElement[1];
            memoryDims = [];
        }

        else
        {
            (memoryData, memoryDims) = WriteUtils.ToMemory<T, TElement>(data);
        }

        // datatype
        var (datatype, encode) =
            DatatypeMessage.Create(Context, memoryData, isScalar, dataset.OpaqueInfo);

        if (dataset.OpaqueInfo is not null && datatype.Class == DatatypeMessageClass.Opaque)
            memoryDims = [(ulong)memoryData.Length / dataset.OpaqueInfo.TypeSize];

        // dataspace
        var fileDims = dataset.FileDims;

        if (fileDims is null)
        {
            if (memoryDims is not null)
            {
                fileDims = dataset.MemorySelection is null || dataset.MemorySelection is AllSelection
                    ? memoryDims
                    : [dataset.MemorySelection.TotalElementCount];
            }
        }

        var dataspace = DataspaceMessage.Create(
            fileDims: fileDims);

        // compact
        //
        // Decided before the filters, because a filter set for every dataset leaves out the smallest
        // datasets, which are written compact instead: compressing a few bytes costs more than it saves,
        // and needs a chunked layout besides. Data given with the dataset, and neither chunks nor filters
        // asked for it, is a candidate.
        var mayBeCompact =
            Context.WriteOptions.PreferCompactDatasetLayout &&
            dataspace.Type != DataspaceType.Null &&
            !memoryData.Equals(default) &&
            dataset.Chunks is null &&
            dataset.DatasetCreation.Filters is null or { Count: 0 };

        var dataEncodeSize = (long)dataspace.Dimensions.Aggregate(1UL, (product, dimension) => product * dimension) * datatype.Size;

        // Variable-length values are placed with a compact header, so whether it can be compact
        // depends on them too. Measured only where that can decide anything.
        var objectSizes = mayBeCompact &&
            datatype.IsReferenceOrContainsReferences() &&
            dataEncodeSize <= Context.WriteOptions.MaximumCompactDatasetSize
                ? MeasureVariableLengthValues(encode, memoryData)
                : null;

        var variableLengthSize = 0L;

        if (objectSizes is not null)
        {
            foreach (var objectSize in objectSizes)
                variableLengthSize += GlobalHeapManager.GetStoredSize(objectSize);
        }

        var fitsCompact = mayBeCompact && DataLayoutMessage4.FitsCompact(Context, dataEncodeSize, variableLengthSize);

        // Below the general bound: by 256 bytes, compressible data is smaller deflated than compact.
        var skipsDefaultFilters = fitsCompact && dataEncodeSize + variableLengthSize <= MAXIMUM_UNFILTERED_DATASET_SIZE;

        // chunk dimensions / filters
        var chunkDimensions = default(uint[]);
        var filters = default(List<H5Filter>);

        if (!isScalar)
        {
            chunkDimensions = dataset.Chunks;

            var localFilters = dataset.DatasetCreation.Filters ?? (skipsDefaultFilters ? null : Context.WriteOptions.Filters);

            // at least one filter is configured - ensure chunked layout
            if (localFilters is not null && localFilters.Any())
            {
                if (chunkDimensions is null)
                {
                    chunkDimensions = dataspace.Dimensions
                        .Select(value => (uint)value)
                        .ToArray();
                }

                filters = localFilters;
            }
        }

        // filter pipeline
        var filterPipeline = default(FilterPipelineMessage);

        if (filters is not null)
            filterPipeline = FilterPipelineMessage.Create(
                dataset,
                datatype.Size,
                chunkDimensions!,
                filters);

        // data layout
        if (chunkDimensions is not null)
        {
            if (dataspace.Dimensions.Length != chunkDimensions.Length)
                throw new Exception("The rank of the chunk dimensions must be equal to the rank of the dataset dimensions.");

            for (int i = 0; i < dataspace.Rank; i++)
            {
                if (chunkDimensions[i] > dataspace.Dimensions[i])
                    throw new Exception("The chunk dimensions must be less than or equal to the dataset dimensions.");
            }
        }

        var dataLayout = DataLayoutMessage4.Create(
            Context,
            typeSize: datatype.Size,
            isFiltered: filterPipeline is not null,
            /* compact data and filtered single chunk index data must not be written deferred because of object header checksum */
            isDeferred: dataspace.Type == DataspaceType.Null ? false : data is null,
            dataDimensions: dataspace.Type == DataspaceType.Null ? default : dataspace.Dimensions,
            chunkDimensions: chunkDimensions,
            variableLengthSize: variableLengthSize);

        // fill value
        var fillValueMessage = DefaultFillValueMessage;

        // header messages
        var headerMessages = new List<HeaderMessage>()
        {
            ToHeaderMessage(datatype),
            ToHeaderMessage(dataspace),
            ToHeaderMessage(dataLayout),
            ToHeaderMessage(fillValueMessage)
        };

        if (filterPipeline is not null)
            headerMessages.Add(ToHeaderMessage(filterPipeline));

        if (dataset.InternalAttributes is not null)
            AppendAttributesToHeaderMessages(dataset.InternalAttributes, headerMessages, Context);

        AppendReferenceCountToHeaderMessages(dataset, headerMessages);

        // object header
        var objectHeader = new ObjectHeader2(
            Address: default,
            Flags: ObjectHeaderFlags.SizeOfChunk1 | ObjectHeaderFlags.SizeOfChunk2,
            AccessTime: default,
            ModificationTime: default,
            ChangeTime: default,
            BirthTime: default,
            MaximumCompactAttributesCount: default,
            MinimumDenseAttributesCount: default,
            HeaderMessages: headerMessages
        )
        {
            Version = 2
        };

        // encode data

        /* dataset info */
        var datasetInfo = new DatasetInfo(
            Space: dataspace,
            Type: datatype,
            Layout: dataLayout,
            FillValue: fillValueMessage,
            FilterPipeline: filterPipeline,
            ExternalFileList: default
        );

        /* buffer provider */
        //
        // Not yet for a chunked dataset whose data comes later: its layout holds what writing it needs -
        // the chunk geometry, the chunks still being filled, the index - about 1 kB, which a writer would
        // otherwise hold for each such dataset from BeginWrite to its first write. See DeferredChunked.
        var isChunked = dataLayout.LayoutClass == LayoutClass.Chunked;

        var h5d = isChunked && memoryData.Equals(default)
            ? null
            : CreateLayout(datasetInfo, dataset.DatasetCreation);

        if (h5d is not null && !memoryData.Equals(default))
        {
            var fileSelection = dataset.FileSelection;
            var memorySelection = dataset.MemorySelection;

            var dims = memoryDims ?? throw new Exception("This should never happen.");

            // The values were measured in memory order above. A write of all of them, unselected, adds them
            // to the heap in that same order, so the measurement can plan its heap rather than be repeated.
            var plannedObjectSizes =
                (fileSelection is null or AllSelection) && (memorySelection is null or AllSelection)
                    ? objectSizes
                    : null;

            // A compact dataset's data is part of its header, so it is structure, and so are the
            // variable-length values it points at. Placed as payload, they would also be named by a
            // staged header with an address that is still provisional.
            if (dataLayout.Properties is CompactStoragePropertyDescription)
                WriteDatasetData(h5d, encode, memoryData, fileSelection, memorySelection, dims, AllocationKind.Metadata);

            // Payload has nowhere final to go while the structure is staged, so it is written once the
            // structure is placed.
            else if (Context.Staging is { } staging)
                PostponeData(staging, dataset, h5d, encode, memoryData, fileSelection, memorySelection, dims, plannedObjectSizes);

            else
                WriteDatasetData(h5d, encode, memoryData, fileSelection, memorySelection, dims, AllocationKind.RawData, plannedObjectSizes);
        }

        DeferredDataset deferred = h5d switch
        {
            null => new DeferredChunked(datasetInfo, encode),

            H5D_Chunk => new DeferredLayout(h5d, encode),

            H5D_Contiguous when dataspace.Type != DataspaceType.Null => new DeferredContiguous(
                fileDims: dataspace.Dimensions,
                type: datatype,
                address: dataLayout.Properties.Address,
                size: ((ContiguousStoragePropertyDescription)dataLayout.Properties).Size,
                encode),

            _ => new DeferredInfo(datasetInfo, encode)
        };

        CollectionsMarshal.GetValueRefOrAddDefault(Context.Objects, dataset, out _).Deferred = deferred;

        /* Note: Ensures that the chunk cache is flushed and all 
         * chunk sizes / addresses are known, before encoding the object header.
         */
        if (h5d is H5D_Chunk chunk)
            chunk.FlushChunkCache();

        // encode object header
        //
        // Only a chunked dataset rewrites its own object header later (H5D_Chunk4.Dispose, once its
        // index is known), so only it keeps a copy of the header to patch.
        if (isChunked)
        {
            var (chunkedAddress, encoded) = objectHeader.EncodeAndKeep(Context);
            Context.DatasetInfoToObjectHeaderMap[datasetInfo] = ((long)chunkedAddress, encoded);

            // The kept copy is not corrected along with the staged header: Dispose rewrites it from the
            // layout's own (relocated) address before it writes it anyway.
            RecordProvisionalAddress(dataLayout.Properties, chunkedAddress, encoded.Length);

            // Data given with the tree may have completed the dataset, whose index can then be written
            // now rather than at Dispose - where it would be one gap per dataset behind everything written
            // since. Not while the structure is staged: the chunk addresses are still provisional.
            if (Context.Staging is null)
                FinishIfComplete(dataset, h5d);

            return chunkedAddress;
        }

        var address = objectHeader.Encode(Context, out var headerLength);

        RecordProvisionalAddress(dataLayout.Properties, address, headerLength);

        return address;
    }

    internal H5D_Base CreateLayout(DatasetInfo datasetInfo, H5DatasetCreation datasetCreation)
    {
        H5D_Base h5d = datasetInfo.Layout.LayoutClass switch
        {
            LayoutClass.Compact => new H5D_Compact(default!, Context, datasetInfo, default),
            LayoutClass.Contiguous => new H5D_Contiguous(default!, Context, datasetInfo, default),
            LayoutClass.Chunked => H5D_Chunk.Create(default!, Context, datasetInfo, default, datasetCreation),

            /* default */
            _ => throw new Exception($"The data layout class '{datasetInfo.Layout.LayoutClass}' is not supported.")
        };

        h5d.Initialize();

        return h5d;
    }

    /// <summary>
    /// Finishes a filtered chunked dataset whose last chunk has been written: writes its index, completes
    /// its header, and drops everything kept to write it.
    /// </summary>
    /// <remarks>
    /// Otherwise all of that waits for Dispose, for every dataset at once - about 1.3 kB per dataset, so
    /// 390 MB over 300,000 datasets written one after another, each long finished. Done between writes
    /// rather than when the chunk is written, since a dataset given with the tree completes its chunks
    /// before its header exists.
    /// </remarks>
    internal void FinishIfComplete(H5Object dataset, H5D_Base? h5d)
    {
        if (h5d is not H5D_Chunk { IsComplete: true } chunk)
            return;

        chunk.Dispose();

        Context.DatasetInfoToObjectHeaderMap.Remove(chunk.Dataset);
        CollectionsMarshal.GetValueRefOrNullRef(Context.Objects, dataset).Deferred = FinishedDataset.Instance;
    }

    /// <summary>
    /// How much global heap space the variable-length values in <paramref name="data" /> would take,
    /// found by encoding them against a target that keeps nothing while the heap only counts.
    /// </summary>
    private List<int> MeasureVariableLengthValues<TElement>(EncodeDelegate<TElement> encode, Memory<TElement> data)
    {
        var heap = Context.GlobalHeapManager;
        var enclosing = heap.AllocationKind;
        List<int> objectSizes;

        // Recording counts a dataset's own values, which are what is being sized here.
        heap.AllocationKind = AllocationKind.RawData;
        heap.BeginRecording();

        try
        {
            encode(data, DiscardWriteStream.Instance);
        }

        finally
        {
            objectSizes = heap.EndRecording();
            heap.AllocationKind = enclosing;
        }

        return objectSizes;
    }

    /// <summary>
    /// Notes where a staged header names provisional payload, so that it is corrected before it is
    /// written. See <see cref="PlaceStructure" />.
    /// </summary>
    private void RecordProvisionalAddress(StoragePropertyDescription properties, ulong headerAddress, int headerLength)
    {
        if (Context.Staging is not { } staging || !Context.FreeSpaceManager.IsProvisional(properties.Address))
            return;

        var headerPosition = (long)Context.Driver.BaseAddress + (long)headerAddress;

        staging.Addresses.Add(new ProvisionalAddress(
            HeaderPosition: headerPosition,
            HeaderLength: headerLength,
            AddressOffset: (int)(properties.AddressPosition - headerPosition)));
    }

    // Separate from InternalEncodeDataset so that the closure is only allocated for a dataset that has
    // data to postpone: a lambda capturing that method's locals would allocate for every dataset.
    private void PostponeData<TElement>(
        StructureStaging staging,
        H5Object dataset,
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        Memory<TElement> memoryData,
        Selection? fileSelection,
        Selection? memorySelection,
        ulong[] memoryDims,
        List<int>? objectSizes)
    {
        staging.PostponedWrites.Add(() =>
        {
            DeferredDataset.RelocateLayout(h5d.Dataset, Context.FreeSpaceManager);

            WriteDatasetData(h5d, encode, memoryData, fileSelection, memorySelection, memoryDims, AllocationKind.RawData, objectSizes);

            if (h5d is H5D_Chunk chunk)
                chunk.FlushChunkCache();

            // The header exists by now, staged and written, so the dataset can be finished like one
            // written through the writer.
            FinishIfComplete(dataset, h5d);
        });
    }

    private void WriteDatasetData<TElement>(
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        Memory<TElement> memoryData,
        Selection? fileSelection,
        Selection? memorySelection,
        ulong[] memoryDims,
        AllocationKind heapAllocationKind,
        List<int>? objectSizes = null)
    {
        // Variable-length elements encoded from here belong to the dataset: payload, unless the dataset
        // is compact and its data is part of its header. Restored rather than left set, because an
        // object reference among these elements encodes the object it points at, attributes and all.
        var enclosing = Context.GlobalHeapManager.AllocationKind;
        Context.GlobalHeapManager.AllocationKind = heapAllocationKind;

        try
        {
            WriteData(h5d, encode, memoryData, fileSelection, memorySelection, memoryDims, objectSizes);
        }

        finally
        {
            Context.GlobalHeapManager.AllocationKind = enclosing;
        }
    }

    private static void InternalWriteDataset<T, TElement>(
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        T data,
        Selection? memorySelection,
        Selection? fileSelection)
    {
        var (memoryData, memoryDims) = WriteUtils.ToMemory<T, TElement>(data);

        if (!memoryData.Equals(default))
        {
            WriteData(
                h5d,
                encode,
                memoryData,
                fileSelection,
                memorySelection,
                memoryDims ?? throw new Exception("This should never happen."));
        }
    }

    // objectSizes: the sizes of the variable-length values the write adds to the heap, in the order it
    // adds them, when they were already measured. Otherwise they are measured here, by a first encode
    // that keeps nothing.
    private static void WriteData<TElement>(
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        Memory<TElement> memoryData,
        Selection? fileSelection,
        Selection? memorySelection,
        ulong[] memoryDims,
        List<int>? objectSizes = null)
    {
        var datasetInfo = h5d.Dataset;
        var dataspace = datasetInfo.Space;
        var datatype = datasetInfo.Type;

        /* buffer provider */
        IH5WriteStream getTargetStream(ulong index) => h5d.GetWriteStream(index);

        /* memory dims */
        memoryDims = h5d.Dataset.Space.Type switch
        {
            DataspaceType.Scalar => [1],
            DataspaceType.Simple => memoryDims,
            _ => throw new Exception($"Unsupported data space type '{h5d.Dataset.Space.Type}'.")
        };

        /* memory selection */
        if (memorySelection is null || memorySelection is AllSelection)
        {
            memorySelection = h5d.Dataset.Space.Type switch
            {
                DataspaceType.Scalar or DataspaceType.Simple => new HyperslabSelection(
                    rank: memoryDims.Length,
                    starts: new ulong[memoryDims.Length],
                    blocks: memoryDims),

                _ => throw new Exception($"Unsupported data space type '{h5d.Dataset.Space.Type}'.")
            };
        }

        /* dataset dims */
        var datasetDims = datasetInfo.Space.GetDims();

        /* dataset chunk dims */
        var datasetChunkDims = h5d.GetChunkDims();

        /* file selection */
        if (fileSelection is null || fileSelection is AllSelection)
        {
            fileSelection = h5d.Dataset.Space.Type switch
            {
                DataspaceType.Scalar or DataspaceType.Simple => new HyperslabSelection(
                    rank: datasetDims.Length,
                    starts: new ulong[datasetDims.Length],
                    blocks: datasetDims),

                _ => throw new Exception($"Unsupported data space type '{h5d.Dataset.Space.Type}'.")
            };
        }

        /* encode info */
        var opaqueSourceTypeSizeFactor = datatype.Class == DatatypeMessageClass.Opaque
            ? datatype.Size
            : 1;

        var encodeInfo = new EncodeInfo<TElement>(
            SourceDims: memoryDims,
            SourceChunkDims: memoryDims,
            TargetDims: datasetDims,
            TargetChunkDims: datasetChunkDims,
            SourceSelection: memorySelection,
            TargetSelection: fileSelection,
            GetSourceBuffer: indiced => memoryData,
            GetTargetStream: getTargetStream,
            Encoder: encode,
            SourceTypeSizeFactor: (int)opaqueSourceTypeSizeFactor,
            TargetTypeSize: (int)datatype.Size,
            AllowBulkCopy: true,
            TargetElementsWritten: h5d is H5D_Chunk chunk ? chunk.OnElementsWritten : null
        );

        /* encode data */
        var context = h5d.WriteContext;

        // Variable-length values are encoded twice: once to measure them, then into a heap sized exactly for
        // them (see GlobalHeapManager.TryBeginPlanned). The measuring encode writes nothing - its target
        // discards. Only for payload: structure shares its collections, see GlobalHeapManager.AddObject.
        // And not when the caller measured them already.
        if (context.GlobalHeapManager.AllocationKind == AllocationKind.RawData &&
            datatype.IsReferenceOrContainsReferences())
        {
            var heap = context.GlobalHeapManager;

            if (objectSizes is null)
            {
                heap.BeginRecording();

                try
                {
                    SelectionHelper.Encode(
                        memoryDims.Length,
                        datasetChunkDims.Length,
                        encodeInfo with
                        {
                            GetTargetStream = _ => DiscardWriteStream.Instance
                        });
                }

                finally
                {
                    objectSizes = heap.EndRecording();
                }
            }

            if (heap.TryBeginPlanned(objectSizes))
            {
                try
                {
                    SelectionHelper.Encode(
                        memoryDims.Length,
                        datasetChunkDims.Length,
                        encodeInfo);
                }

                catch
                {
                    heap.AbandonPlanned();
                    throw;
                }

                heap.EndPlanned();

                return;
            }
        }

        SelectionHelper.Encode(
            memoryDims.Length,
            datasetChunkDims.Length,
            encodeInfo);
    }

    /// <summary>
    /// A target that accepts writes and keeps none of them, for an encode that is run only to measure it.
    /// </summary>
    private sealed class DiscardWriteStream : IH5WriteStream
    {
        public static DiscardWriteStream Instance { get; } = new();

        public long Position => 0;

        public void Seek(long offset, SeekOrigin origin)
        {
        }

        public void WriteDataset(Span<byte> buffer)
        {
        }

        public void Dispose()
        {
        }
    }

    private static void AppendAttributesToHeaderMessages(
        Dictionary<string, object> attributes,
        List<HeaderMessage> headerMessages,
        NativeWriteContext context)
    {
        // TODO https://forum.hdfgroup.org/t/hdf5-file-format-is-attribute-info-message-required/11277
        // attribute info message
        if (attributes.Any())
        {
            var attributeInfoMessage = new AttributeInfoMessage(
                Flags: CreationOrderFlags.None,
                MaximumCreationIndex: default,
                FractalHeapAddress: Superblock.UndefinedAddress,
                BTree2NameIndexAddress: Superblock.UndefinedAddress,
                BTree2CreationOrderIndexAddress: Superblock.UndefinedAddress
            )
            {
                Version = 0
            };

            headerMessages.Add(ToHeaderMessage(attributeInfoMessage));
        }

        // attribute messages
        foreach (var entry in attributes)
        {
            var attributeMessage = AttributeMessage.Create(context, entry.Key, entry.Value);

            headerMessages.Add(ToHeaderMessage(attributeMessage));
        }
    }

    /// <summary>
    /// Rejects a link name that HDF5 cannot represent.
    /// </summary>
    /// <remarks>
    /// HDF5 reserves '/' as the path separator, so a link name containing one cannot be traversed: the
    /// C library tools report the object as missing rather than reading a link of that name, which makes
    /// the whole file unreadable to them even though PureHDF can still open it.
    /// <para>
    /// Checked here, at the point the link is written, because the group API is an
    /// <see cref="IDictionary{TKey, TValue}" /> whose keys are set by plain assignment - there is no
    /// call to fail earlier in, and failing at write time still fails before anything is encoded.
    /// </para>
    /// <para>
    /// ATTRIBUTE names are not checked, and must not be: an attribute name is not a path component, and
    /// the C library accepts a slash in one.
    /// </para>
    /// </remarks>
    private static void ValidateLinkName(string name)
    {
        if (name.Contains('/'))
            throw new ArgumentException(
                $"The link name '{name}' contains '/', which HDF5 reserves as the path separator. "
                + "Nest the object in an H5Group instead.");
    }

    private static HeaderMessage ToHeaderMessage(Message message)
    {
        var type = message switch
        {
            NilMessage => MessageType.NIL,
            DataspaceMessage => MessageType.Dataspace,
            LinkInfoMessage => MessageType.LinkInfo,
            DatatypeMessage => MessageType.Datatype,
            OldFillValueMessage => MessageType.OldFillValue,
            FillValueMessage => MessageType.FillValue,
            LinkMessage => MessageType.Link,
            ExternalFileListMessage => MessageType.ExternalDataFiles,
            DataLayoutMessage => MessageType.DataLayout,
            BogusMessage => MessageType.Bogus,
            GroupInfoMessage => MessageType.GroupInfo,
            FilterPipelineMessage => MessageType.FilterPipeline,
            AttributeMessage => MessageType.Attribute,
            ObjectCommentMessage => MessageType.ObjectComment,
            OldObjectModificationTimeMessage => MessageType.OldObjectModificationTime,
            SharedMessageTableMessage => MessageType.SharedMessageTable,
            ObjectHeaderContinuationMessage => MessageType.ObjectHeaderContinuation,
            SymbolTableMessage => MessageType.SymbolTable,
            ObjectModificationMessage => MessageType.ObjectModification,
            BTreeKValuesMessage => MessageType.BTreeKValues,
            DriverInfoMessage => MessageType.DriverInfo,
            AttributeInfoMessage => MessageType.AttributeInfo,
            ObjectReferenceCountMessage => MessageType.ObjectReferenceCount,
            _ => throw new NotSupportedException($"The message type '{message.GetType().FullName}' is not supported.")
        };

        return new HeaderMessage(
            Type: type,
            DataSize: default /* TODO maybe this can be determined statically (reduces number of Stream.Seek operations) */,
            Flags: MessageFlags.NoFlags,
            CreationOrder: default,
            Data: message
        )
        {
            Version = 2,
            WithCreationOrder = default
        };
    }
}
