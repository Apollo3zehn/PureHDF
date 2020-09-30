using System;

namespace HDF5.NET
{
    public class LinkMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public LinkMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // flags
            this.Flags = reader.ReadByte();

            // link type
            var isLinkTypeFieldPresent = (this.Flags & (1 << 3)) > 0;

            if (isLinkTypeFieldPresent)
                this.LinkType = (LinkType)reader.ReadByte();

            // creation order
            var isCreationOrderFieldPresent = (this.Flags & (1 << 2)) > 0;

            if (isCreationOrderFieldPresent)
                this.CreationOrder = reader.ReadUInt64();

            // link name encoding
            var isLinkNameEncodingFieldPresent = (this.Flags & (1 << 4)) > 0;

            if (isLinkNameEncodingFieldPresent)
                this.LinkNameEncoding = (CharacterSetEncoding)reader.ReadByte();

            // link length
            var linkLengthFieldLength = (ulong)(1 << (this.Flags & 0x03));
            var linkNameLength = H5Utils.ReadUlong(this.Reader, linkLengthFieldLength);

            // link name
            this.LinkName = H5Utils.ReadFixedLengthString(reader, (int)linkNameLength, this.LinkNameEncoding);

            // link info
            this.LinkInfo = this.LinkType switch
            {
                LinkType.Hard       => new HardLinkInfo(reader, superblock),
                LinkType.Soft       => new SoftLinkInfo(reader),
                LinkType.External   => new ExternalLinkInfo(reader),
                _ when (65 <= (byte)this.LinkType && (byte)this.LinkType <= 255) => new UserDefinedLinkInfo(reader),
                _ => throw new NotSupportedException($"The link message link type '{this.LinkType}' is not supported.")
            };
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
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(LinkMessage)} are supported.");

                _version = value;
            }
        }

        public byte Flags { get; set; }
        public LinkType LinkType { get; set; }
        public ulong CreationOrder { get; set; }
        public CharacterSetEncoding LinkNameEncoding { get; set; }
        public string LinkName { get; set; }
        public LinkInfo LinkInfo { get; set; }

        #endregion
    }
}
