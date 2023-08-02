namespace PureHDF;

internal static class H5Writer
{
    public static void Serialize(H5File file, string filePath, H5SerializerOptions options)
    {
        using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        using var driver = new BinaryWriter(fileStream);

        var freeSpaceManager = new FreeSpaceManager();
        freeSpaceManager.Allocate(Superblock23.ENCODE_SIZE);

        var globalHeapManager = new GlobalHeapManager(options, freeSpaceManager, driver);

        var writeContext = new WriteContext(
            Driver: driver,
            FreeSpaceManager: freeSpaceManager,
            GlobalHeapManager: globalHeapManager,
            SerializerOptions: options,
            TypeToMessageMap: new(),
            ObjectToAddressMap: new()
        );

        // root group       
        driver.BaseStream.Seek(Superblock23.ENCODE_SIZE, SeekOrigin.Begin);
        var rootGroupAddress = EncodeGroup(writeContext, file);

        // global heap collections
        globalHeapManager.Encode();

        // superblock
        var endOfFileAddress = (ulong)driver.BaseStream.Length;

        var superblock = new Superblock23(
            Driver: default!,
            Version: 3,
            FileConsistencyFlags: default,
            BaseAddress: 0,
            ExtensionAddress: Superblock.UndefinedAddress,
            EndOfFileAddress: endOfFileAddress,
            RootGroupObjectHeaderAddress: rootGroupAddress)
        {
            OffsetsSize = sizeof(ulong),
            LengthsSize = sizeof(ulong)
        };

        driver.BaseStream.Seek(0, SeekOrigin.Begin);
        superblock.Encode(driver);
    }

    private static ulong EncodeGroup(
        WriteContext context,
        H5Group group)
    {
        var headerMessages = new List<HeaderMessage>();

        // link info message
        var linkInfoMessage = new LinkInfoMessage(
            Context: default,
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
                default,
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
            var attributeMessage = AttributeMessage.Create(context, entry.Key, entry.Value);

            headerMessages.Add(ToHeaderMessage(attributeMessage));
        }

        foreach (var entry in group)
        {
            ulong linkAddress;

            if (entry.Value is H5Group childGroup)
            {
                if (!context.ObjectToAddressMap.TryGetValue(childGroup, out linkAddress))
                {
                    linkAddress = EncodeGroup(context, childGroup);
                    context.ObjectToAddressMap[childGroup] = linkAddress;
                }
            }

            else if (entry.Value is H5Dataset dataset)
            {
                if (!context.ObjectToAddressMap.TryGetValue(dataset, out linkAddress))
                {
                    linkAddress = EncodeDataset(context, dataset);
                    context.ObjectToAddressMap[dataset] = linkAddress;
                }
            }

            else if (entry.Value is object objectDataset)
            {
                if (!context.ObjectToAddressMap.TryGetValue(objectDataset, out linkAddress))
                {
                    linkAddress = EncodeDataset(context, objectDataset);
                    context.ObjectToAddressMap[objectDataset] = linkAddress;
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
        var address = objectHeader.Encode(context);

        return address;
    }

    private static ulong EncodeDataset(
        WriteContext context,
        object dataset)
    {
        var (data, chunkDimensions) = dataset is H5Dataset h5Dataset1
            ? (h5Dataset1.Data, h5Dataset1.ChunkDimensions)
            : (dataset, default);

        (data, var dataDimensions) = WriteUtils.EnsureMemoryOrScalar(data);
        var type = data.GetType();

        // datatype
        var (datatype, encode) = 
            DatatypeMessage.Create(context, type, data);

        // dataspace
        var dataspace = dataset is H5Dataset h5Dataset2
            ? DataspaceMessage.Create(dataDimensions, h5Dataset2.Dimensions)
            : DataspaceMessage.Create(dataDimensions, default);

        if (chunkDimensions is not null)
        {
            if (dataspace.DimensionSizes.Length != chunkDimensions.Length)
                throw new Exception("The rank of the chunk dimensions must be equal to the rank of the dataset dimensions.");

            for (int i = 0; i < dataspace.Rank; i++)
            {
                if (chunkDimensions[i] > dataspace.DimensionSizes[i])
                    throw new Exception("The chunk dimensions must be less than or equal to the dataset dimensions.");
            }
        }

        // data encode size
        ulong dataEncodeSize;

        if (chunkDimensions is null)
        {
            dataEncodeSize = datatype.Size * dataspace.DimensionSizes
                .Aggregate(1UL, (product, dimension) => product * dimension);
        }

        else
        {
            var chunkSize = chunkDimensions
                .Aggregate(1UL, (product, dimension) => product * dimension);

            dataEncodeSize = 1;

            for (int dimension = 0; dimension < dataspace.Rank; dimension++)
            {
                dataEncodeSize *= (ulong)Math
                    .Ceiling(dataspace.DimensionSizes[dimension] / (double)chunkDimensions[dimension]);
            }

            dataEncodeSize *= chunkSize * datatype.Size;
        }
        
        // data layout
        var dataLayout = DataLayoutMessage4.Create(
            context, 
            encode, 
            dataEncodeSize, 
            data,
            chunkDimensions);

        // fill value
        var fillValue = new byte[datatype.Size]; /* "The default fill value is 0 (zero), ..." (https://docs.hdfgroup.org/hdf5/develop/group___d_c_p_l.html) */

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
        if (dataLayout.Properties is ContiguousStoragePropertyDescription contiguous)
        {
            var driver = context.Driver;
            driver.BaseStream.Seek((long)contiguous.Address, SeekOrigin.Begin);

            encode.Invoke(driver.BaseStream, data);
        }

        else if (dataLayout.Properties is ChunkedStoragePropertyDescription4 chunked)
        {
            var driver = context.Driver;
            driver.BaseStream.Seek((long)chunked.Address, SeekOrigin.Begin);

            if (chunked.IndexingTypeInformation is ImplicitIndexingInformation @implicit)
            {
                encode.Invoke(driver.BaseStream, data);
            }

            else
            {
                throw new Exception($"The indexing type {chunked.IndexingTypeInformation.GetType()} is not supported.");
            }
        }

        // encode object header
        var address = objectHeader.Encode(context);

        return address;
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