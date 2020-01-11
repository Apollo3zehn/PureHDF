using System;
using System.IO;

namespace HDF5.NET
{
    public class AttributeMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public AttributeMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // flags (only version 2 and 3)
            this.Flags = (AttributeMessageFlags)reader.ReadByte();

            // reserved
            reader.ReadByte();

            // name size
            this.NameSize = reader.ReadUInt16();

            // datatype size
            this.DatatypeSize = reader.ReadUInt16();

            // dataspace size
            this.DataspaceSize = reader.ReadUInt16();

            // name character set encoding
            this.NameCharacterSetEncoding = (CharacterSetEncoding)reader.ReadByte();

            // name
            this.Name = H5Utils.ReadNullTerminatedString(reader, pad: true, this.NameCharacterSetEncoding);

            // datatype
#warning Insert padding bytes! But only if version == 1!
            this.Datatype = new DatatypeMessage(reader);

            // dataspace 
#warning Insert padding bytes! But only if version == 1!
            this.Dataspace = new DataspaceMessage(reader, superblock);

            // data
#warning determine size correctly from datatype and dataspace
            this.Data = reader.ReadBytes(1);
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
                if (!(1 <= value && value <= 3))
                    throw new FormatException($"Only version 1 - 3 instances of type {nameof(AttributeMessage)} are supported.");

                _version = value;
            }
        }

        public AttributeMessageFlags Flags { get; set; }
        public ushort NameSize { get; set; }
        public ushort DatatypeSize { get; set; }
        public ushort DataspaceSize { get; set; }
        public CharacterSetEncoding NameCharacterSetEncoding { get; set; }
        public string Name { get; set; }
        public DatatypeMessage Datatype { get; set; }
        public DataspaceMessage Dataspace { get; set; }
        public byte[] Data { get; set; }

        #endregion
    }
}
