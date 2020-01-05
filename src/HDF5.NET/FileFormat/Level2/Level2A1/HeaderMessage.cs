using System;
using System.IO;

namespace HDF5.NET
{
    public class HeaderMessage : FileBlock
    {
        #region Constructors

        public HeaderMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Type = (HeaderMessageType)reader.ReadUInt16();
            this.DataSize = reader.ReadUInt16();
            this.Flags = (HeaderMessageFlags)reader.ReadByte();
            reader.ReadBytes(3);

            this.Data = this.Type switch
            {
                HeaderMessageType.NIL => new NilMessage(reader),
                HeaderMessageType.Dataspace => new DataspaceMessage(reader),
                HeaderMessageType.LinkInfo => new LinkInfoMessage(reader),
                HeaderMessageType.DataType => new DatatypeMessage(reader),
                HeaderMessageType.OldFillValue => new OldFillValueMessage(reader),
                HeaderMessageType.FillValue => new FillValueMessage(reader),
                HeaderMessageType.Link => new LinkMessage(reader),
                HeaderMessageType.ExternalDataFiles => new ExternalFileListMessage(reader),
                //HeaderMessageType.DataLayout => new DataLayoutMessage(reader),
                HeaderMessageType.Bogus => new BogusMessage(reader),
                HeaderMessageType.GroupInfo => new GroupInfoMessage(reader),
                HeaderMessageType.FilterPipeline => new FilterPipelineMessage(reader),
                HeaderMessageType.Attribute => new AttributeMessage(reader),
                HeaderMessageType.ObjectComment => new ObjectCommentMessage(reader),
                HeaderMessageType.OldObjectModificationTime => new OldObjectModificationTimeMessage(reader),
                HeaderMessageType.SharedMessageTable => new SharedMessageTableMessage(reader),
                HeaderMessageType.ObjectHeaderContinuation => new ObjectHeaderContinuationMessage(reader),
                HeaderMessageType.SymbolTable => new SymbolTableMessage(reader, superblock),
                HeaderMessageType.ObjectModification => new ObjectModificationMessage(reader),
                HeaderMessageType.BTreeKValues => new BTreeKValuesMessage(reader),
                HeaderMessageType.DriverInfo => new DriverInfoMessage(reader),
                HeaderMessageType.AttributeInfo => new AttributeInfoMessage(reader),
                HeaderMessageType.ObjectReferenceCount => new ObjectReferenceCountMessage(reader),
                _ => throw new NotSupportedException($"The message type '{this.Type}' is not supported.")
            };

            this.TotalMessageSize = 2 + 2 + 1 + 3 + this.DataSize;
        }

        #endregion

        #region Properties

        public int TotalMessageSize { get; }

        public HeaderMessageType Type { get; set; }
        public ushort DataSize { get; set; }
        public HeaderMessageFlags Flags { get; set; }
        public Message Data { get; set; }

        #endregion
    }
}
