using PureHDF.Selections;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace PureHDF;

partial class H5NativeWriter
{
    private static readonly MethodInfo _methodInfoEncodeDataset = typeof(H5NativeWriter)
        .GetMethod(nameof(InternalEncodeDataset), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo _methodInfoWriteDataset = typeof(H5NativeWriter)
        .GetMethod(nameof(InternalWriteDataset), BindingFlags.NonPublic | BindingFlags.Static)!;

    private ulong _rootGroupAddress;

    /// <summary>
    /// Totals up how many bytes of structure <paramref name="file" /> needs, by encoding it against a
    /// stream that discards everything.
    /// </summary>
    /// <remarks>
    /// Exact rather than estimated, because it is the same encoder and the same allocator - so it
    /// accounts for the things an estimate cannot, above all the global heap's 4 kB collection
    /// granularity and a chunk index's dependence on chunk count. It does not compress, since no
    /// metadata size depends on compressed output; see <see cref="NativeWriteContext.SizeOnly" />.
    /// <para>
    /// Chunk indexes are allocated when the writer is disposed rather than during encoding, so the total
    /// is only complete after Dispose has run - hence the explicit call rather than a using block.
    /// </para>
    /// </remarks>
    internal static long MeasureMetadataSize(H5File file, H5WriteOptions options)
    {
        var sizingOptions = options with
        {
            // Interleaved, so that this pass allocates no region of its own and the total it reports is
            // purely the sum of what the file needs.
            MetadataPlacement = H5MetadataPlacement.Interleaved,
            MetadataReservation = 0
        };

        using var stream = new SizingStream();

        var writer = new H5NativeWriter(file, stream, sizingOptions, leaveOpen: true, sizeOnly: true);

        writer.Write();
        writer.Dispose();

        return writer.Context.FreeSpaceManager.MetadataAllocated;
    }

    internal H5NativeWriter(H5File file, Stream stream, H5WriteOptions options, bool leaveOpen)
        : this(file, stream, options, leaveOpen, sizeOnly: false)
    {
        //
    }

    private H5NativeWriter(H5File file, Stream stream, H5WriteOptions options, bool leaveOpen, bool sizeOnly)
    {
        // TODO writing never reads from the stream (checksums are computed from the encoded bytes), so the
        // readable requirement could be lifted.
        if (!stream.CanRead || !stream.CanWrite || !stream.CanSeek)
            throw new Exception("The stream must be readble, writable and seekable.");

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
            options.MetadataReservation == 0 &&
            !sizeOnly;

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

        else if (options.MetadataPlacement == H5MetadataPlacement.FrontLoaded && !sizeOnly)
        {
            // No reservation given: measure the file instead of guessing at it. The sizing pass runs
            // Interleaved, so it cannot recurse back into this branch.
            //
            // The superblock is subtracted because the measurement totals every metadata allocation
            // INCLUDING it, while the superblock is allocated before the region exists and so is served
            // from outside it - leaving the region longer than anything will ever ask of it by exactly
            // that much. Subtracting makes the reservation the exact number of bytes that will be served
            // from it, which is what lets a front-loaded file come out the same size as an interleaved
            // one rather than merely close to it.
            var reservation = options.MetadataReservation > 0
                ? options.MetadataReservation
                : MeasureMetadataSize(file, options) - Superblock23.ENCODE_SIZE;

            freeSpaceManager.ReserveMetadataRegion(reservation);
        }

        var globalHeapManager = new GlobalHeapManager(options, freeSpaceManager, driver);

        var writeContext = new NativeWriteContext(
            Writer: this,
            File: file,
            Driver: driver,
            FreeSpaceManager: freeSpaceManager,
            GlobalHeapManager: globalHeapManager,
            WriteOptions: options,
            DatasetToInfoMap: new(),
            DatasetInfoToObjectHeaderMap: new(),
            TypeToMessageMap: new(),
            ObjectToAddressMap: new(),
            ObjectReferenceCountMap: new(),
            RawValueToDatasetMap: new(ReferenceEqualityComparer.Instance),
            ShortlivedStream: new(memory: default)
        )
        {
            SizeOnly = sizeOnly,
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
        CountReferences(File, Context.ObjectReferenceCountMap);

        // root group
        Context.Driver.SeekRelativeToBaseAddress(Superblock23.ENCODE_SIZE);
        _rootGroupAddress = EncodeGroup(File);

        PlaceStructure();
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
        foreach (var (h5d, _) in Context.DatasetToInfoMap.Values)
            RelocateLayout(h5d.Dataset, freeSpaceManager);

        foreach (var write in staging.PostponedWrites)
            write();
    }

    private void CountReferences(H5Group root, Dictionary<H5Object, int> counts)
    {
        var visitedGroups = new HashSet<H5Group>();

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

                counts.TryGetValue(h5Object, out var current);
                counts[h5Object] = current + 1;

                if (entry.Value is H5Group childGroup && visitedGroups.Add(childGroup))
                    Walk(childGroup);
            }
        }

        Walk(root);
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

            ulong linkAddress;

            LinkType linkType;
            LinkInfo linkInfo;

            if (entry.Value is H5SoftLink softLink)
            {
                linkType = LinkType.Soft;
                linkInfo = new SoftLinkInfo(softLink.Target);
            }

            else
            {
                var h5Object = GetH5Object(entry.Value);

                if (!Context.ObjectToAddressMap.TryGetValue(h5Object, out linkAddress))
                {
                    Context.ObjectToAddressMap[h5Object] = default;

                    if (entry.Value is H5Group childGroup)
                        linkAddress = EncodeGroup(childGroup);

                    else if (entry.Value is H5Dataset dataset1)
                        linkAddress = EncodeDataset(dataset1);

                    else
                        linkAddress = EncodeDataset((H5Dataset)h5Object);

                    Context.ObjectToAddressMap[h5Object] = linkAddress;
                }

                else if (linkAddress == default)
                {
                    throw new Exception("The current object is already being encoded which suggests a circular reference.");
                }

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
        var (elementType, isScalar) = WriteUtils.GetElementType(dataset.Type);

        // TODO cache this
        var method = _methodInfoEncodeDataset.MakeGenericMethod(dataset.Type, elementType);

        try
        {
            return (ulong)method.Invoke(this, [dataset, dataset.Data, isScalar])!;
        }

        // Rethrown unwrapped, preserving the original stack: encoding is where a caller's own data is
        // rejected, and reflection would otherwise replace a specific, actionable message with
        // "Exception has been thrown by the target of an invocation".
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable; the compiler cannot see that the line above always throws
        }
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

        // chunk dimensions / filters
        var chunkDimensions = default(uint[]);
        var filters = default(List<H5Filter>);

        if (!isScalar)
        {
            chunkDimensions = dataset.Chunks;

            var localFilters = dataset.DatasetCreation.Filters ?? Context.WriteOptions.Filters;

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
            chunkDimensions: chunkDimensions);

        // fill value
        /* "The default fill value is 0 (zero), ..." (https://docs.hdfgroup.org/hdf5/develop/group___d_c_p_l.html) */
        var fillValueMessage = new FillValueMessage(
            AllocationTime: SpaceAllocationTime.Early,
            FillTime: FillValueWriteTime.Never,
            Value: default
        )
        {
            Version = 3
        };

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
        H5D_Base h5d = dataLayout.LayoutClass switch
        {
            LayoutClass.Compact => new H5D_Compact(default!, Context, datasetInfo, default),
            LayoutClass.Contiguous => new H5D_Contiguous(default!, Context, datasetInfo, default),
            LayoutClass.Chunked => H5D_Chunk.Create(default!, Context, datasetInfo, default, dataset.DatasetCreation),

            /* default */
            _ => throw new Exception($"The data layout class '{dataLayout.LayoutClass}' is not supported.")
        };

        h5d.Initialize();

        if (!memoryData.Equals(default) && (!Context.SizeOnly || SizingPassNeedsData<TElement>(dataLayout)))
        {
            var fileSelection = dataset.FileSelection;
            var memorySelection = dataset.MemorySelection;

            var dims = memoryDims ?? throw new Exception("This should never happen.");

            // A compact dataset's data is part of its header, so it is structure, and so are the
            // variable-length values it points at. Placed as payload, they would also be named by a
            // staged header with an address that is still provisional.
            if (dataLayout.Properties is CompactStoragePropertyDescription)
                WriteDatasetData(h5d, encode, memoryData, fileSelection, memorySelection, dims, AllocationKind.Metadata);

            // Payload has nowhere final to go while the structure is staged, so it is written once the
            // structure is placed.
            else if (Context.Staging is { } staging)
                PostponeData(staging, h5d, encode, memoryData, fileSelection, memorySelection, dims);

            else
                WriteDatasetData(h5d, encode, memoryData, fileSelection, memorySelection, dims, AllocationKind.RawData);
        }

        Context.DatasetToInfoMap[dataset] = (h5d, encode);

        /* Note: Ensures that the chunk cache is flushed and all 
         * chunk sizes / addresses are known, before encoding the object header.
         */
        if (h5d is H5D_Chunk chunk)
            chunk.FlushChunkCache();

        // encode object header
        //
        // Only a chunked dataset rewrites its own object header later (H5D_Chunk4.Dispose, once its
        // index is known), so only it keeps a copy of the header to patch.
        if (h5d is H5D_Chunk4)
        {
            var (chunkedAddress, encoded) = objectHeader.EncodeAndKeep(Context);
            Context.DatasetInfoToObjectHeaderMap[datasetInfo] = ((long)chunkedAddress, encoded);

            // The kept copy is not corrected along with the staged header: Dispose rewrites it from the
            // layout's own (relocated) address before it writes it anyway.
            RecordProvisionalAddress(dataLayout.Properties, chunkedAddress, encoded.Length);

            return chunkedAddress;
        }

        var address = objectHeader.Encode(Context, out var headerLength);

        RecordProvisionalAddress(dataLayout.Properties, address, headerLength);

        return address;
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

    /// <summary>
    /// Moves a provisionally placed payload address to where it ended up. See
    /// <see cref="FreeSpaceManager.ReserveProvisionalRegion" />.
    /// </summary>
    private static void RelocateLayout(DatasetInfo info, FreeSpaceManager freeSpaceManager)
    {
        if (info.Layout is DataLayoutMessage4 layout)
            layout.Properties.Address = freeSpaceManager.Relocate(layout.Properties.Address);
    }

    // Separate from InternalEncodeDataset so that the closure is only allocated for a dataset that has
    // data to postpone: a lambda capturing that method's locals would allocate for every dataset.
    private void PostponeData<TElement>(
        StructureStaging staging,
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        Memory<TElement> memoryData,
        Selection? fileSelection,
        Selection? memorySelection,
        ulong[] memoryDims)
    {
        staging.PostponedWrites.Add(() =>
        {
            RelocateLayout(h5d.Dataset, Context.FreeSpaceManager);

            WriteDatasetData(h5d, encode, memoryData, fileSelection, memorySelection, memoryDims, AllocationKind.RawData);

            if (h5d is H5D_Chunk chunk)
                chunk.FlushChunkCache();
        });
    }

    private void WriteDatasetData<TElement>(
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        Memory<TElement> memoryData,
        Selection? fileSelection,
        Selection? memorySelection,
        ulong[] memoryDims,
        AllocationKind heapAllocationKind)
    {
        // Variable-length elements encoded from here belong to the dataset: payload, unless the dataset
        // is compact and its data is part of its header. Restored rather than left set, because an
        // object reference among these elements encodes the object it points at, attributes and all.
        var enclosing = Context.GlobalHeapManager.AllocationKind;
        Context.GlobalHeapManager.AllocationKind = heapAllocationKind;

        try
        {
            WriteData(h5d, encode, memoryData, fileSelection, memorySelection, memoryDims);
        }

        finally
        {
            Context.GlobalHeapManager.AllocationKind = enclosing;
        }
    }

    /// <summary>
    /// Whether a sizing pass has to encode this dataset's data, or can leave it alone.
    /// </summary>
    /// <remarks>
    /// The pass exists to total up metadata, so it only has to do work that decides how much metadata
    /// there is. Copying fixed-size payload into a layout decides nothing: a contiguous region and an
    /// implicit chunk index are raw data, a fixed-array index is sized from the chunk count, and every
    /// index's encoded width comes from the dataspace rather than from a value. So for fixed-size
    /// elements the pass skips the whole data path - no selection walk, no chunk buffers, no byte
    /// copying, no raw chunk allocation - on top of already skipping compression.
    /// <para>
    /// What it cannot skip is data whose encoding allocates. That is why the test is on TElement rather
    /// than on the dataset type: an array is itself a reference, so asking this of the dataset type
    /// answers yes for every array-backed dataset and the pass would skip nothing.
    /// </para>
    /// </remarks>
    private static bool SizingPassNeedsData<TElement>(DataLayoutMessage4 dataLayout)
    {
        // A compact dataset's payload is embedded in its object header, so here the data IS metadata.
        // Nothing to save either way - compact is capped at 64 kB.
        if (dataLayout.Properties is CompactStoragePropertyDescription)
            return true;

        // Anything holding a reference goes through the per-element encoder, which allocates: a
        // variable-length string or sequence takes global heap space sized from the values themselves,
        // and an object reference encodes the object it points at, header and all.
        if (DataUtils.IsReferenceOrContainsReferences(typeof(TElement)))
            return true;

        // Nullable<T> is encoded as a variable-length sequence of one, so it takes global heap space
        // too - and contains no reference, so the check above does not see it.
        return Nullable.GetUnderlyingType(typeof(TElement)) is not null;
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

    private static void WriteData<TElement>(
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        Memory<TElement> memoryData,
        Selection? fileSelection,
        Selection? memorySelection,
        ulong[] memoryDims)
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
            AllowBulkCopy: true
        );

        /* encode data */
        SelectionHelper.Encode(
            memoryDims.Length,
            datasetChunkDims.Length,
            encodeInfo);
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
