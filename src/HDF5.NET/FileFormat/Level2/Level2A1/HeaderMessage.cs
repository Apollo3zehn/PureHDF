using System;
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

        internal HeaderMessage(H5Context context, byte version, bool withCreationOrder = false) : base(context.Reader)
        {
            this.Version = version;
            this.WithCreationOrder = withCreationOrder;

            // version
            if (version == 1)
                this.Type = (HeaderMessageType)context.Reader.ReadUInt16();
            else if (version == 2)
                this.Type = (HeaderMessageType)context.Reader.ReadByte();

            // data size
            this.DataSize = context.Reader.ReadUInt16();

            // flags
            this.Flags = (HeaderMessageFlags)context.Reader.ReadByte();

            // reserved / creation order
            if (version == 1)
                context.Reader.ReadBytes(3);
            else if (version == 2 && withCreationOrder)
                this.CreationOrder = context.Reader.ReadUInt16();

            // data
            var readerPosition1 = context.Reader.BaseStream.Position;

            this.Data = this.Type switch
            {
                HeaderMessageType.NIL                       => new NilMessage(context.Reader),
                HeaderMessageType.Dataspace                 => new DataspaceMessage(context.Reader, context.Superblock),
                HeaderMessageType.LinkInfo                  => new LinkInfoMessage(context.Reader, context.Superblock),
                HeaderMessageType.Datatype                  => new DatatypeMessage(context.Reader),
                HeaderMessageType.OldFillValue              => new OldFillValueMessage(context.Reader),
                HeaderMessageType.FillValue                 => new FillValueMessage(context.Reader),
                HeaderMessageType.Link                      => new LinkMessage(context.Reader, context.Superblock),
                HeaderMessageType.ExternalDataFiles         => new ExternalFileListMessage(context.Reader, context.Superblock),
                HeaderMessageType.DataLayout                => DataLayoutMessage.Construct(context.Reader, context.Superblock),
                HeaderMessageType.Bogus                     => new BogusMessage(context.Reader),
                HeaderMessageType.GroupInfo                 => new GroupInfoMessage(context.Reader),
                HeaderMessageType.FilterPipeline            => new FilterPipelineMessage(context.Reader),
                HeaderMessageType.Attribute                 => new AttributeMessage(context.Reader, context.Superblock),
                HeaderMessageType.ObjectComment             => new ObjectCommentMessage(context.Reader),
                HeaderMessageType.OldObjectModificationTime => new OldObjectModificationTimeMessage(context.Reader),
                HeaderMessageType.SharedMessageTable        => new SharedMessageTableMessage(context.Reader, context.Superblock),
                HeaderMessageType.ObjectHeaderContinuation  => new ObjectHeaderContinuationMessage(context.Reader, context.Superblock),
                HeaderMessageType.SymbolTable               => new SymbolTableMessage(context.Reader, context.Superblock),
                HeaderMessageType.ObjectModification        => new ObjectModificationMessage(context.Reader),
                HeaderMessageType.BTreeKValues              => new BTreeKValuesMessage(context.Reader),
                HeaderMessageType.DriverInfo                => new DriverInfoMessage(context.Reader),
                HeaderMessageType.AttributeInfo             => new AttributeInfoMessage(context.Reader, context.Superblock),
                HeaderMessageType.ObjectReferenceCount      => new ObjectReferenceCountMessage(context.Reader),
                _ => throw new NotSupportedException($"The message type '{this.Type}' is not supported.")
            };

            var readerPosition2 = context.Reader.BaseStream.Position;
            var paddingBytes = this.DataSize - (readerPosition2 - readerPosition1);

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
                if (this.Version == 1 && value)
                    throw new FormatException("Only version 2 header messages are allowed to have 'WithCreationOrder' set to true.");

                _withCreationOrder = value;
            }
        }

        public HeaderMessageType Type { get; set; }
        public ushort DataSize { get; set; }
        public HeaderMessageFlags Flags { get; set; }
        public ushort CreationOrder { get; set; }
        public Message Data { get; set; }

        #endregion
    }
}
