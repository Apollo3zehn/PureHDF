using System.Diagnostics;

namespace PureHDF
{
    [DebuggerDisplay("{Type}")]
    internal class HeaderMessage
    {
        #region Fields

        private byte _version;
        private bool _withCreationOrder;

        #endregion

        #region Constructors

        internal HeaderMessage(H5Context context, byte version, ObjectHeader objectHeader, bool withCreationOrder = false)
        {
            Version = version;
            WithCreationOrder = withCreationOrder;

            // version
            if (version == 1)
                Type = (MessageType)context.Driver.ReadUInt16();
            else if (version == 2)
                Type = (MessageType)context.Driver.ReadByte();

            // data size
            DataSize = context.Driver.ReadUInt16();

            // flags
            Flags = (MessageFlags)context.Driver.ReadByte();

            // reserved / creation order
            if (version == 1)
                context.Driver.ReadBytes(3);

            else if (version == 2 && withCreationOrder)
                CreationOrder = context.Driver.ReadUInt16();

            // data
            var driverPosition1 = context.Driver.Position;

            /* Search for "H5O_SHARED_DECODE_REAL" in C-code to find all shareable messages */

            Data = Type switch
            {
                MessageType.NIL => new NilMessage(),
                MessageType.Dataspace => objectHeader.DecodeMessage(Flags, () => new DataspaceMessage(context)),
                MessageType.LinkInfo => new LinkInfoMessage(context),
                MessageType.Datatype => objectHeader.DecodeMessage(Flags, () => new DatatypeMessage(context.Driver)),
                MessageType.OldFillValue => objectHeader.DecodeMessage(Flags, () => new OldFillValueMessage(context.Driver)),
                MessageType.FillValue => objectHeader.DecodeMessage(Flags, () => new FillValueMessage(context.Driver)),
                MessageType.Link => new LinkMessage(context),
                MessageType.ExternalDataFiles => new ExternalFileListMessage(context),
                MessageType.DataLayout => DataLayoutMessage.Construct(context),
                MessageType.Bogus => new BogusMessage(context.Driver),
                MessageType.GroupInfo => new GroupInfoMessage(context.Driver),
                MessageType.FilterPipeline => objectHeader.DecodeMessage(Flags, () => new FilterPipelineMessage(context.Driver)),
                MessageType.Attribute => objectHeader.DecodeMessage(Flags, () => new AttributeMessage(context, objectHeader)),
                MessageType.ObjectComment => new ObjectCommentMessage(context.Driver),
                MessageType.OldObjectModificationTime => new OldObjectModificationTimeMessage(context.Driver).ToObjectModificationMessage(),
                MessageType.SharedMessageTable => new SharedMessageTableMessage(context),
                MessageType.ObjectHeaderContinuation => new ObjectHeaderContinuationMessage(context),
                MessageType.SymbolTable => new SymbolTableMessage(context),
                MessageType.ObjectModification => new ObjectModificationMessage(context.Driver),
                MessageType.BTreeKValues => new BTreeKValuesMessage(context.Driver),
                MessageType.DriverInfo => new DriverInfoMessage(context.Driver),
                MessageType.AttributeInfo => new AttributeInfoMessage(context),
                MessageType.ObjectReferenceCount => new ObjectReferenceCountMessage(context.Driver),
                _ => throw new NotSupportedException($"The message type '{Type}' is not supported.")
            };

            var driverPosition2 = context.Driver.Position;
            var paddingBytes = DataSize - (driverPosition2 - driverPosition1);

            context.Driver.ReadBytes((int)paddingBytes);
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
