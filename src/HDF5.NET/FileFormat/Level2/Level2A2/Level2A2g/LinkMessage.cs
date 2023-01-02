namespace HDF5.NET
{
    internal class LinkMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public LinkMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            Version = reader.ReadByte();

            // flags
            Flags = reader.ReadByte();

            // link type
            var isLinkTypeFieldPresent = (Flags & (1 << 3)) > 0;

            if (isLinkTypeFieldPresent)
                LinkType = (LinkType)reader.ReadByte();

            // creation order
            var isCreationOrderFieldPresent = (Flags & (1 << 2)) > 0;

            if (isCreationOrderFieldPresent)
                CreationOrder = reader.ReadUInt64();

            // link name encoding
            var isLinkNameEncodingFieldPresent = (Flags & (1 << 4)) > 0;

            if (isLinkNameEncodingFieldPresent)
                LinkNameEncoding = (CharacterSetEncoding)reader.ReadByte();

            // link length
            var linkLengthFieldLength = (ulong)(1 << (Flags & 0x03));
            var linkNameLength = H5Utils.ReadUlong(Reader, linkLengthFieldLength);

            // link name
            LinkName = H5ReadUtils.ReadFixedLengthString(reader, (int)linkNameLength, LinkNameEncoding);

            // link info
            LinkInfo = LinkType switch
            {
                LinkType.Hard       => new HardLinkInfo(reader, superblock),
                LinkType.Soft       => new SoftLinkInfo(reader),
                LinkType.External   => new ExternalLinkInfo(reader),
                _ when (65 <= (byte)LinkType && (byte)LinkType <= 255) => new UserDefinedLinkInfo(reader),
                _ => throw new NotSupportedException($"The link message link type '{LinkType}' is not supported.")
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
