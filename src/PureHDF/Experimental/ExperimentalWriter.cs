namespace PureHDF.Experimental;

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
        
        // TODO FIX THIS (workaround to object header data)
        freeSpaceManager.Allocate(1024);

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
        WriteContext writeContext,
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
            var attributeMessage = CreateAttributeMessage(writeContext, entry.Key, entry.Value);

            headerMessages.Add(ToHeaderMessage(attributeMessage));
        }

        foreach (var entry in group)
        {
            if (entry.Value is H5Group childGroup)
            {
                if (!writeContext.ObjectToAddressMap.TryGetValue(childGroup, out var childAddress))
                {
                    childAddress = EncodeGroup(writeContext, childGroup);
                    writeContext.ObjectToAddressMap[childGroup] = childAddress;
                }

                var linkMessage = new LinkMessage(
                    Flags: LinkInfoFlags.LinkNameLengthSizeUpperBit | LinkInfoFlags.LinkNameEncodingFieldIsPresent,
                    LinkType: default,
                    CreationOrder: default,
                    LinkName: entry.Key,
                    LinkInfo: new HardLinkInfo(HeaderAddress: childAddress)
                )
                {
                    Version = 1
                };

                headerMessages.Add(ToHeaderMessage(linkMessage));
            }
        }

        var address = (ulong)writeContext.Driver.BaseStream.Position;

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

        objectHeader.Encode(writeContext);

        return address;
    }

    private static AttributeMessage CreateAttributeMessage(
        WriteContext context,
        string name, 
        object attribute)
    {
        // datatype
        var data = attribute is H5Attribute h5Attribute1
            ? h5Attribute1.Data
            : attribute;

        var type = data.GetType();

        var (dataType, dataDimensions, encode) = DatatypeMessage
            .Create(context, type, data);

        // dataspace
        var dimensions = attribute is H5Attribute h5Attribute2
            ? h5Attribute2.Dimensions ?? dataDimensions
            : dataDimensions;

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

        // attribute
        var attributeMessage = new AttributeMessage(
            Flags: AttributeMessageFlags.None,
            Name: name,
            Datatype: dataType,
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
}