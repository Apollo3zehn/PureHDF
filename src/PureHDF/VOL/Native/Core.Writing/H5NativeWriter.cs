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

        var driver = new H5StreamDriver(stream, leaveOpen: leaveOpen);

        var freeSpaceManager = new FreeSpaceManager();
        freeSpaceManager.Allocate(Superblock23.ENCODE_SIZE);

        var globalHeapManager = new GlobalHeapManager(options, freeSpaceManager, driver);

        var writeContext = new NativeWriteContext(
            File: file,
            Driver: driver,
            FreeSpaceManager: freeSpaceManager,
            GlobalHeapManager: globalHeapManager,
            WriteOptions: options,
            DatasetToInfoMap: new(),
            TypeToMessageMap: new(),
            ObjectToAddressMap: new(),
            ShortlivedStream: new(memory: default)
        );

        File = file;
        Stream = stream;
        Context = writeContext;
    }

    internal H5File File { get; }

    internal Stream Stream { get; }

    internal NativeWriteContext Context { get; }

    internal void Write()
    {
        // root group       
        Context.Driver.Seek(Superblock23.ENCODE_SIZE, SeekOrigin.Begin);
        _rootGroupAddress = EncodeGroup(File);

        // global heap collections
        Context.GlobalHeapManager.Encode();
    }

    private ulong EncodeGroup(
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

        // TODO https://forum.hdfgroup.org/t/hdf5-file-format-is-attribute-info-message-required/11277
        // attribute info message
        if (group.Attributes.Any())
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
        foreach (var entry in group.Attributes)
        {
            var attributeMessage = AttributeMessage.Create(Context, entry.Key, entry.Value);

            headerMessages.Add(ToHeaderMessage(attributeMessage));
        }

        foreach (var entry in group)
        {
            ulong linkAddress;

            if (entry.Value is H5Group childGroup)
            {
                if (!Context.ObjectToAddressMap.TryGetValue(childGroup, out linkAddress))
                {
                    linkAddress = EncodeGroup(childGroup);
                    Context.ObjectToAddressMap[childGroup] = linkAddress;
                }
            }

            else if (entry.Value is H5Dataset dataset)
            {
                if (!Context.ObjectToAddressMap.TryGetValue(dataset, out linkAddress))
                {
                    linkAddress = EncodeDataset(dataset);
                    Context.ObjectToAddressMap[dataset] = linkAddress;
                }
            }

            else if (entry.Value is object objectDataset)
            {
                if (!Context.ObjectToAddressMap.TryGetValue(objectDataset, out linkAddress))
                {
                    linkAddress = EncodeDataset(objectDataset);
                    Context.ObjectToAddressMap[objectDataset] = linkAddress;
                }
            }

            else
            {
                throw new Exception("This should never happen.");
            }

            var linkMessage = new LinkMessage(
                Flags: LinkInfoFlags.LinkNameLengthSizeUpperBit | LinkInfoFlags.LinkNameEncodingFieldIsPresent,
                LinkType: default,
                CreationOrder: default,
                LinkName: entry.Key,
                LinkInfo: new HardLinkInfo(HeaderAddress: linkAddress)
            )
            {
                Version = 1
            };

            headerMessages.Add(ToHeaderMessage(linkMessage));
        }

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
        var address = objectHeader.Encode(Context);

        return address;
    }

    private ulong EncodeDataset(
        object dataset)
    {
        if (dataset is not H5Dataset h5Dataset)
            h5Dataset = new H5Dataset(dataset);

        var (elementType, isScalar) = WriteUtils.GetElementType(h5Dataset.Type);

        // TODO cache this
        var method = _methodInfoEncodeDataset.MakeGenericMethod(h5Dataset.Type, elementType);

        return (ulong)method.Invoke(this, new object?[] { h5Dataset, h5Dataset.Data, isScalar })!;
    }

    private ulong InternalEncodeDataset<T, TElement>(
        H5Dataset dataset,
        T data,
        bool isScalar)
    {
        var (memoryData, dataDimensions) = WriteUtils.ToMemory<T, TElement>(data);

        // datatype
        var (datatype, encode) = 
            DatatypeMessage.Create(Context, memoryData, isScalar);

        // dataspace
        var dataspace = dataset is H5Dataset h5Dataset
            ? DataspaceMessage.Create(dataDimensions, h5Dataset.Dimensions)
            : DataspaceMessage.Create(dataDimensions, default);

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
            /* compact data must be written immediately because of object header checksum */
            allowCompact: data is not null,
            dataDimensions: dataspace.Dimensions,
            chunkDimensions: chunkDimensions);

        // fill value
        /* "The default fill value is 0 (zero), ..." (https://docs.hdfgroup.org/hdf5/develop/group___d_c_p_l.html) */
        var fillValue = new byte[datatype.Size];

        var fillValueMessage = new FillValueMessage(
            AllocationTime: SpaceAllocationTime.Early,
            FillTime: FillValueWriteTime.Never,
            Value: fillValue
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
            WriteData(h5d, encode, memoryData);

        Context.DatasetToInfoMap[dataset] = (h5d, encode);

        /* Note: Eensures that the chunk cache is flushed and all 
         * chunk sizes / addresses are known, before encoding the object header.
         */
        if (h5d is H5D_Chunk chunk)
            chunk.FlushChunkCache();

        // encode object header
        var address = objectHeader.Encode(Context);

        return address;
    }

    private static void InternalWriteDataset<T, TElement>(
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        T data)
    {
        var (memoryData, _) = WriteUtils.ToMemory<T, TElement>(data);

        WriteData(h5d, encode, memoryData);
    }

    private static void WriteData<TElement>(
        H5D_Base h5d,
        EncodeDelegate<TElement> encode,
        Memory<TElement> memoryData)
    {
        var datasetInfo = h5d.Dataset;
        var dataspace = datasetInfo.Space;
        var datatype = datasetInfo.Type;

        /* buffer provider */
        IH5WriteStream getTargetStream(ulong[] indices) => h5d.GetWriteStream(indices);

        /* memory selection */
        var memoryDimensions = dataspace.Dimensions.Length == 0 
            ? new ulong[] { 1 } 
            : dataspace.Dimensions;

        var memoryStarts = memoryDimensions.ToArray();
        memoryStarts.AsSpan().Clear();

        var memorySelection = new HyperslabSelection(
            rank: memoryDimensions.Length, 
            starts: memoryStarts,
            blocks: memoryDimensions);

        /* file dims */
        var fileDims = datasetInfo.GetDatasetDims();

        /* file chunk dims */
        var fileChunkDims = h5d.GetChunkDims();

        /* file selection */
        var fileStarts = fileDims.ToArray();
        fileStarts.AsSpan().Clear();

        var fileSelection = new HyperslabSelection(rank: fileDims.Length, starts: fileStarts, blocks: fileDims);

        var encodeInfo = new EncodeInfo<TElement>(
            SourceDims: memoryDimensions,
            SourceChunkDims: memoryDimensions,
            TargetDims: fileDims,
            TargetChunkDims: fileChunkDims,
            SourceSelection: memorySelection,
            TargetSelection: fileSelection,
            GetSourceBuffer: indiced => memoryData,
            GetTargetStream: getTargetStream,
            Encoder: encode,
            TargetTypeSize: (int)datatype.Size,
            SourceTypeFactor: 1
        );

        /* encode data */
        SelectionUtils.Encode(memorySelection.Rank, fileSelection.Rank, encodeInfo);
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