namespace PureHDF.Experimental;

internal static class H5Writer
{
    public static void Serialize(H5File file, string filePath)
    {
        using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var driver = new BinaryWriter(fileStream);

        // encode superblock
        var objectHeaderAddress = 
            (ulong)Superblock.FormatSignature.Length + 
            sizeof(byte) + sizeof(byte) + sizeof(byte) + sizeof(byte) + 
            sizeof(ulong) + sizeof(ulong) + sizeof(ulong) + sizeof(ulong) +
            sizeof(uint);

        var superblock = new Superblock23(
            Driver: default!,
            Version: 3,
            FileConsistencyFlags: default,
            BaseAddress: 0,
            ExtensionAddress: Superblock.UndefinedAddress,
            EndOfFileAddress: default, /* TODO write correct value */
            RootGroupObjectHeaderAddress: objectHeaderAddress,
            Checksum: default /* TODO write correct value, H5F_super_cache.c -> H5F_sblock_flush */)
        {
            OffsetsSize = sizeof(ulong),
            LengthsSize = sizeof(ulong)
        };

        superblock.Encode(driver);

        // root group
        EncodeGroup(driver, file);
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
            DataSize: default /* TODO write correct value */,
            Flags: MessageFlags.NoFlags,
            CreationOrder: default,
            Data: message
        )
        {
            Version = 2,
            WithCreationOrder = default
        };
    }

    private static void EncodeGroup(BinaryWriter driver, H5Group group)
    {
        var headerMessages = new List<HeaderMessage>();

        var linkInfoMessage = new LinkInfoMessage(
            Context: default,
            Flags: default,
            MaximumCreationIndex: default,
            FractalHeapAddress: default,
            BTree2NameIndexAddress: default,
            BTree2CreationOrderIndexAddress: default
        )
        {
            Version = 0
        };

        headerMessages.Add(ToHeaderMessage(linkInfoMessage));

        foreach (var child in group.Objects)
        {
            var linkMessage = new LinkMessage(
                Flags: LinkInfoFlags.LinkNameLengthSizeUpperBit | LinkInfoFlags.LinkNameEncodingFieldIsPresent,
                LinkType: default,
                CreationOrder: default,
                LinkName: child.Name,
                LinkInfo: new HardLinkInfo(HeaderAddress: default /* TODO write correct value */)
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
            SizeOfChunk0: default /* TODO write correct value */,
            HeaderMessages: headerMessages
        )
        {
            Version = 2
        };

        objectHeader.Encode(driver);
    }
}