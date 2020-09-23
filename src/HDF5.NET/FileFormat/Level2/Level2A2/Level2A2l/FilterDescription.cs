using System;

namespace HDF5.NET
{
    public class FilterDescription : FileBlock
    {
        #region Constructors

        public FilterDescription(H5BinaryReader reader, byte version) : base(reader)
        {
            // filter identifier
            this.Identifier = (FilterIdentifier)reader.ReadInt16();

            // name length
            this.NameLength = version switch
            {
                1                                               => reader.ReadUInt16(),
                2 when (ushort)this.Identifier >= 256     => reader.ReadUInt16(),
                2 when (ushort)this.Identifier < 256      => 0,
                _ => throw new NotSupportedException($"Only version 1 or 2 instances of the {nameof(FilterDescription)} type are supported.")
            };

            // flags
            this.Flags = (FilterFlags)reader.ReadUInt16();

            // client data value count
            var clientDataValueCount = reader.ReadUInt16();

            // name
            this.Name = this.NameLength > 0 ? H5Utils.ReadNullTerminatedString(reader, pad: true) : string.Empty;

            // client data
            this.ClientData = new uint[clientDataValueCount];

            for (ushort i = 0; i < clientDataValueCount; i++)
            {
                this.ClientData[i] = reader.ReadUInt32();
            }

            // padding
            if (version == 1 && clientDataValueCount % 2 != 0)
                reader.ReadBytes(4);
        }

        #endregion

        #region Properties

        public FilterIdentifier Identifier { get; set; }
        public ushort NameLength { get; set; }
        public FilterFlags Flags { get; set; }
        public string Name { get; set; }
        public uint[] ClientData { get; set; }

        #endregion
    }
}
