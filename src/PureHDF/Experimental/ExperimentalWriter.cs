namespace PureHDF.Experimental;

internal static class H5Writer
{
    public static void Serialize(H5File file, string filePath)
    {
        var objectToAddressMap = new Dictionary<H5Object, ulong>();

        using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        using var driver = new BinaryWriter(fileStream);

        // root group
        driver.BaseStream.Seek(Superblock23.SIZE, SeekOrigin.Begin);
        var rootGroupAddress = EncodeGroup(driver, file, objectToAddressMap);

        // superblock
        var endOfFileAddress = (ulong)driver.BaseStream.Position;
        driver.BaseStream.Seek(0, SeekOrigin.Begin);

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

        superblock.Encode(driver);
    }

    private static ulong EncodeGroup(
        BinaryWriter driver,
        H5Group group,
        Dictionary<H5Object, ulong> objectToAddressMap)
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
            var attributeMessage = CreateAttributeMessage(entry.Key, entry.Value);

            headerMessages.Add(ToHeaderMessage(attributeMessage));
        }

        foreach (var entry in group)
        {
            if (entry.Value is H5Group childGroup)
            {
                if (!objectToAddressMap.TryGetValue(childGroup, out var childAddress))
                {
                    childAddress = EncodeGroup(driver, childGroup, objectToAddressMap);
                    objectToAddressMap[childGroup] = childAddress;
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

        var address = (ulong)driver.BaseStream.Position;

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

        objectHeader.Encode(driver);

        return address;
    }

    private static AttributeMessage CreateAttributeMessage(string name, H5AttributeBase attribute)
    {
        // datatype
        var dataType = DatatypeMessage.Create(attribute.Type, attribute.TypeSize);

        // dataspace
        var dimensions = attribute.Dimensions ?? new ulong[] { 
            (ulong)(attribute.Data.Length / attribute.TypeSize)
        };

        var dataspace = new DataspaceMessage(
            Rank: (byte)dimensions.Length,
            Flags: DataspaceMessageFlags.None,
            Type: DataspaceType.Simple,
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
            Data: attribute.Data
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