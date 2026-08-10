using PureHDF.Selections;
using System.Reflection;

namespace PureHDF;

partial class H5NativeWriter
{
    private static readonly MethodInfo _methodInfoEncodeDataset = typeof(H5NativeWriter)
        .GetMethod(nameof(InternalEncodeDataset), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo _methodInfoWriteDataset = typeof(H5NativeWriter)
        .GetMethod(nameof(InternalWriteDataset), BindingFlags.NonPublic | BindingFlags.Static)!;

    private ulong _rootGroupAddress;

    internal H5NativeWriter(H5File file, Stream stream, H5WriteOptions options, bool leaveOpen)
    {
        // TODO readable is only required for checksums, maybe this requirement can be lifted by renting Memory<byte> and calculate the checksum over that memory
        if (!stream.CanRead || !stream.CanWrite || !stream.CanSeek)
            throw new Exception("The stream must be readble, writable and seekable.");

        // allowPositionless: false - the write path writes at the wrapped stream's own cursor, so a
        // driver that kept the cursor to itself (which is what IDatasetStream enables on the read
        // side) would write at the wrong offsets. IDatasetStream is a read-side interface; this keeps
        // a stream that happens to implement it writing exactly as it did before.
        var driver = new H5StreamDriver(stream, leaveOpen: leaveOpen, allowPositionless: false);

        if (options.UserBlockSize != 0)
        {
            /* https://stackoverflow.com/a/600306/1636629 */
            var isPowerOfTwo = (options.UserBlockSize & (options.UserBlockSize - 1)) == 0;

            if (isPowerOfTwo && options.UserBlockSize >= 512)
                driver.SetBaseAddress(options.UserBlockSize);

            else
                throw new Exception("The user block size is invalid.");
        }

        var freeSpaceManager = new FreeSpaceManager();
        freeSpaceManager.Allocate(Superblock23.ENCODE_SIZE);

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
        );

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
            Context: default!,
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
        // NOTE (async propagation): ObjectHeader2.Encode() is now async (it reads
        // back the just-written bytes to compute a checksum). This method and its
        // public entry point (H5File.Write(), no async counterpart exists) stay
        // synchronous, so the call is bridged here — see report.
        var address = objectHeader.Encode(Context).GetAwaiter().GetResult();

        return address;
    }

    internal ulong EncodeDataset(
        H5Dataset dataset)
    {
        var (elementType, isScalar) = WriteUtils.GetElementType(dataset.Type);

        // TODO cache this
        var method = _methodInfoEncodeDataset.MakeGenericMethod(dataset.Type, elementType);

        return (ulong)method.Invoke(this, [dataset, dataset.Data, isScalar])!;
    }

    private ulong InternalEncodeDataset<T, TElement>(
        H5Dataset dataset,
        T data,
        bool isScalar)
    {
        var (memoryData, memoryDims) = WriteUtils.ToMemory<T, TElement>(data);

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

        if (!memoryData.Equals(default))
        {
            WriteData(
                h5d,
                encode,
                memoryData,
                dataset.FileSelection,
                dataset.MemorySelection,
                memoryDims ?? throw new Exception("This should never happen."));
        }

        Context.DatasetToInfoMap[dataset] = (h5d, encode);

        /* Note: Ensures that the chunk cache is flushed and all 
         * chunk sizes / addresses are known, before encoding the object header.
         */
        if (h5d is H5D_Chunk chunk)
            chunk.FlushChunkCache();

        // encode object header
        // NOTE (async propagation): see the matching note in EncodeGroup above.
        var address = objectHeader.Encode(Context).GetAwaiter().GetResult();
        var end = (ulong)Context.Driver.Position - Context.Driver.BaseAddress;

        Context.DatasetInfoToObjectHeaderMap[datasetInfo] = ((long)address, (int)(end - address));

        return address;
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
                default!,
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