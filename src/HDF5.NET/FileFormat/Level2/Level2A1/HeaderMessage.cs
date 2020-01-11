using System;
using System.IO;

namespace HDF5.NET
{
    public class HeaderMessage : FileBlock
    {
        #region Fields

        private byte _version;
        private bool _withCreationOrder;

        #endregion

        #region Constructors

        public HeaderMessage(BinaryReader reader, Superblock superblock, byte version, bool withCreationOrder = false) : base(reader)
        {
            this.Version = version;
            this.WithCreationOrder = withCreationOrder;

            // version
            if (version == 1)
                this.Type = (HeaderMessageType)reader.ReadUInt16();
            else if (version == 2)
                this.Type = (HeaderMessageType)reader.ReadByte();

            // data size
            this.DataSize = reader.ReadUInt16();

            // flags
            this.Flags = (HeaderMessageFlags)reader.ReadByte();

            // reserved / creation order
            if (version == 1)
                reader.ReadBytes(3);
            else if (version == 2 && withCreationOrder)
                this.CreationOrder = reader.ReadUInt16();

            // data
            var readerPosition1 = reader.BaseStream.Position;

            this.Data = this.Type switch
            {
                HeaderMessageType.NIL                       => new NilMessage(reader),
                HeaderMessageType.Dataspace                 => new DataspaceMessage(reader, superblock),
                HeaderMessageType.LinkInfo                  => new LinkInfoMessage(reader, superblock),
                HeaderMessageType.DataType                  => new DatatypeMessage(reader),
                HeaderMessageType.OldFillValue              => new OldFillValueMessage(reader),
                HeaderMessageType.FillValue                 => new FillValueMessage(reader),
                HeaderMessageType.Link                      => new LinkMessage(reader, superblock),
                HeaderMessageType.ExternalDataFiles         => new ExternalFileListMessage(reader, superblock),
                HeaderMessageType.DataLayout                => DataLayoutMessage.Construct(reader, superblock),
                HeaderMessageType.Bogus                     => new BogusMessage(reader),
                HeaderMessageType.GroupInfo                 => new GroupInfoMessage(reader),
                HeaderMessageType.FilterPipeline            => new FilterPipelineMessage(reader),
                HeaderMessageType.Attribute                 => new AttributeMessage(reader, superblock),
                HeaderMessageType.ObjectComment             => new ObjectCommentMessage(reader),
                HeaderMessageType.OldObjectModificationTime => new OldObjectModificationTimeMessage(reader),
                HeaderMessageType.SharedMessageTable        => new SharedMessageTableMessage(reader, superblock),
                HeaderMessageType.ObjectHeaderContinuation  => new ObjectHeaderContinuationMessage(reader, superblock),
                HeaderMessageType.SymbolTable               => new SymbolTableMessage(reader, superblock),
                HeaderMessageType.ObjectModification        => new ObjectModificationMessage(reader),
                HeaderMessageType.BTreeKValues              => new BTreeKValuesMessage(reader),
                HeaderMessageType.DriverInfo                => new DriverInfoMessage(reader),
                HeaderMessageType.AttributeInfo             => new AttributeInfoMessage(reader, superblock),
                HeaderMessageType.ObjectReferenceCount      => new ObjectReferenceCountMessage(reader),
                _ => throw new NotSupportedException($"The message type '{this.Type}' is not supported.")
            };

            var readerPosition2 = reader.BaseStream.Position;
            var paddingBytes = this.DataSize - (readerPosition2 - readerPosition1);

            reader.ReadBytes((int)paddingBytes);
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
