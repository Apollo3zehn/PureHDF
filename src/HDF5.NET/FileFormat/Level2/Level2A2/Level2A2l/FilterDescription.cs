namespace HDF5.NET
{
    internal class FilterDescription : FileBlock
    {
        #region Constructors

        public FilterDescription(H5BinaryReader reader, byte version) : base(reader)
        {
            // filter identifier
            Identifier = (FilterIdentifier)reader.ReadInt16();

            // name length
            var nameLength = version switch
            {
                1                                               => reader.ReadUInt16(),
                2 when (ushort)Identifier >= 256     => reader.ReadUInt16(),
                2 when (ushort)Identifier < 256      => 0,
                _ => throw new NotSupportedException($"Only version 1 or 2 instances of the {nameof(FilterDescription)} type are supported.")
            };

            // flags
            Flags = (FilterFlags)reader.ReadUInt16();

            // client data value count
            var clientDataValueCount = reader.ReadUInt16();

            // name
            Name = nameLength > 0 ? H5ReadUtils.ReadNullTerminatedString(reader, pad: true) : string.Empty;

            // client data
            ClientData = new uint[clientDataValueCount];

            for (ushort i = 0; i < clientDataValueCount; i++)
            {
                ClientData[i] = reader.ReadUInt32();
            }

            // padding
            if (version == 1 && clientDataValueCount % 2 != 0)
                reader.ReadBytes(4);
        }

        #endregion

        #region Properties

        public FilterIdentifier Identifier { get; set; }
        public FilterFlags Flags { get; set; }
        public string Name { get; set; }
        public uint[] ClientData { get; set; }

        #endregion
    }
}
