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
            var attributeMessage = CreateAttributeMessage(context, entry.Key, entry.Value);

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
        var data = dataset is H5Dataset h5Dataset1
            ? h5Dataset1.Data
            : dataset;

        var (datatype, dataspace, encode) = dataset is H5Dataset h5Dataset2
            ? GetDataMessages(context, data, h5Dataset2.Dimensions)
            : GetDataMessages(context, data, default);

        var dataEncodeSize = datatype.Size * dataspace.DimensionSizes
            .Aggregate(1UL, (product, dimension) => product * dimension);

        var dataLayout = CreateLayoutMessage(context, encode, dataEncodeSize, data);

        var headerMessages = new List<HeaderMessage>()
        {
            ToHeaderMessage(datatype),
            ToHeaderMessage(dataspace),
            ToHeaderMessage(dataLayout)
        };

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

        // encode object header
        var address = objectHeader.Encode(context);

        return address;
    }

    private static DataLayoutMessage4 CreateLayoutMessage(
        WriteContext context,
        EncodeDelegate encode,
        ulong dataEncodeSize,
        object data)
    {
        // TODO: The ushort.MaxValue limit is not stated in the specification but
        // makes sense because of the size field of the Compact Storage Property
        // Description.
        //
        // See also H5Dcompact.c (H5D__compact_construct): "Verify data size is 
        // smaller than maximum header message size (64KB) minus other layout 
        // message fields."

        var isChunked = false;
        var preferCompact = context.SerializerOptions.PreferCompactDatasetLayout;
        var dataLayout = default(DataLayoutMessage4);

        if (isChunked)
        {
            throw new NotImplementedException();
        }

        else
        {
            /* try to create compact dataset */
            if (preferCompact && dataEncodeSize <= ushort.MaxValue)
            {
                var properties = new CompactStoragePropertyDescription(
                    InputData: default!,
                    EncodeData: driver => encode(driver.BaseStream, data),
                    EncodeDataSize: (ushort)dataEncodeSize
                );

                dataLayout = new DataLayoutMessage4(
                    LayoutClass: LayoutClass.Compact,
                    Address: default,
                    Properties: properties
                )
                {
                    Version = 4
                };

                var dataLayoutEncodeSize = dataLayout.GetEncodeSize();

                if (dataEncodeSize + dataLayoutEncodeSize > ushort.MaxValue)
                    dataLayout = default;
            }

            /* create contiguous dataset */
            if (dataLayout == default)
            {
                var address = context.FreeSpaceManager.Allocate((long)dataEncodeSize);

                var properties = new ContiguousStoragePropertyDescription(
                    Address: (ulong)address,
                    Size: dataEncodeSize
                );

                dataLayout = new DataLayoutMessage4(
                    LayoutClass: LayoutClass.Contiguous,
                    Address: default,
                    Properties: properties
                )
                {
                    Version = 4
                };
            }
        }

        return dataLayout;
    }

    private static AttributeMessage CreateAttributeMessage(
        WriteContext context,
        string name, 
        object attribute)
    {
        var data = attribute is H5Attribute h5Attribute1
            ? h5Attribute1.Data
            : attribute;

        var (datatype, dataspace, encode) = attribute is H5Attribute h5Attribute2
            ? GetDataMessages(context, data, h5Attribute2.Dimensions)
            : GetDataMessages(context, data, default);

        // attribute
        var attributeMessage = new AttributeMessage(
            Flags: AttributeMessageFlags.None,
            Name: name,
            Datatype: datatype,
            Dataspace: dataspace,
            InputData: default,
            EncodeData: writer => encode(writer.BaseStream, data)
        )
        {
            Version = 3
        };

        return attributeMessage;
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

    private static (DatatypeMessage, DataspaceMessage, EncodeDelegate) GetDataMessages(WriteContext context, object data, ulong[]? dimensions)
    {
        // datatype
        var type = data.GetType();

        var (dataType, dataDimensions, encode) = DatatypeMessage
            .Create(context, type, data);

        // dataspace
        dimensions ??= dataDimensions;

        var dimensionsTotalSize = dimensions
            .Aggregate(1UL, (x, y) => x * y);

        var dataDimensionsTotalSize = dataDimensions
            .Aggregate(1UL, (x, y) => x * y);

        if (dataDimensions.Any() && dimensionsTotalSize != dataDimensionsTotalSize)
            throw new Exception("The actual number of elements does not match the total number of elements given in the dimensions parameter.");

        var dataspace = new DataspaceMessage(
            Rank: (byte)dimensions.Length,
            Flags: DataspaceMessageFlags.None,
            Type: dataDimensions.Any() ? DataspaceType.Simple : DataspaceType.Scalar,
            DimensionSizes: dimensions,
            DimensionMaxSizes: dimensions,
            PermutationIndices: default
        )
        {
            Version = 2
        };

        return (dataType, dataspace, encode);
    }
}