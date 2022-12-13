using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Type}")]
    internal class HeaderMessage : FileBlock
    {
        #region Fields

        private byte _version;
        private bool _withCreationOrder;

        #endregion

        #region Constructors

        internal HeaderMessage(H5Context context, byte version, ObjectHeader objectHeader, bool withCreationOrder = false) : base(context.Reader)
        {
            Version = version;
            WithCreationOrder = withCreationOrder;

            // version
            if (version == 1)
                Type = (MessageType)context.Reader.ReadUInt16();
            else if (version == 2)
                Type = (MessageType)context.Reader.ReadByte();

            // data size
            DataSize = context.Reader.ReadUInt16();

            // flags
            Flags = (MessageFlags)context.Reader.ReadByte();

            // reserved / creation order
            if (version == 1)
                context.Reader.ReadBytes(3);

            else if (version == 2 && withCreationOrder)
                CreationOrder = context.Reader.ReadUInt16();

            // data
            var readerPosition1 = context.Reader.BaseStream.Position;

            /* Search for "H5O_SHARED_DECODE_REAL" in C-code to find all shareable messages */

            Data = Type switch
            {
                MessageType.NIL                         => new NilMessage(context.Reader),
                MessageType.Dataspace                   => objectHeader.DecodeMessage(Flags, () => new DataspaceMessage(context.Reader, context.Superblock)),
                MessageType.LinkInfo                    => new LinkInfoMessage(context.Reader, context.Superblock),
                MessageType.Datatype                    => objectHeader.DecodeMessage(Flags, () => new DatatypeMessage(context.Reader)),
                MessageType.OldFillValue                => objectHeader.DecodeMessage(Flags, () => new OldFillValueMessage(context.Reader)),
                MessageType.FillValue                   => objectHeader.DecodeMessage(Flags, () => new FillValueMessage(context.Reader)),
                MessageType.Link                        => new LinkMessage(context.Reader, context.Superblock),
                MessageType.ExternalDataFiles           => new ExternalFileListMessage(context.Reader, context.Superblock),
                MessageType.DataLayout                  => DataLayoutMessage.Construct(context.Reader, context.Superblock),
                MessageType.Bogus                       => new BogusMessage(context.Reader),
                MessageType.GroupInfo                   => new GroupInfoMessage(context.Reader),
                MessageType.FilterPipeline              => objectHeader.DecodeMessage(Flags, () => new FilterPipelineMessage(context.Reader)),
                MessageType.Attribute                   => objectHeader.DecodeMessage(Flags, () => new AttributeMessage(context, objectHeader)),
                MessageType.ObjectComment               => new ObjectCommentMessage(context.Reader),
                MessageType.OldObjectModificationTime   => new OldObjectModificationTimeMessage(context.Reader).ToObjectModificationMessage(),
                MessageType.SharedMessageTable          => new SharedMessageTableMessage(context.Reader, context.Superblock),
                MessageType.ObjectHeaderContinuation    => new ObjectHeaderContinuationMessage(context.Reader, context.Superblock),
                MessageType.SymbolTable                 => new SymbolTableMessage(context.Reader, context.Superblock),
                MessageType.ObjectModification          => new ObjectModificationMessage(context.Reader),
                MessageType.BTreeKValues                => new BTreeKValuesMessage(context.Reader),
                MessageType.DriverInfo                  => new DriverInfoMessage(context.Reader),
                MessageType.AttributeInfo               => new AttributeInfoMessage(context.Reader, context.Superblock),
                MessageType.ObjectReferenceCount        => new ObjectReferenceCountMessage(context.Reader),
                _ => throw new NotSupportedException($"The message type '{Type}' is not supported.")
            };

            var readerPosition2 = context.Reader.BaseStream.Position;
            var paddingBytes = DataSize - (readerPosition2 - readerPosition1);

            context.Reader.ReadBytes((int)paddingBytes);
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (!(1 <= value && value <= 2))
                    throw new NotSupportedException("The header message version number must be in the range of 1..2.");

                _version = value;
            }
        }

        public bool WithCreationOrder 
        { 
            get
            {
                return _withCreationOrder;
            }
            set
            {
                if (Version == 1 && value)
                    throw new FormatException("Only version 2 header messages are allowed to have 'WithCreationOrder' set to true.");

                _withCreationOrder = value;
            }
        }

        public MessageType Type { get; set; }
        public ushort DataSize { get; set; }
        public MessageFlags Flags { get; set; }
        public ushort CreationOrder { get; set; }
        public Message Data { get; set; }

        #endregion
    }
}
