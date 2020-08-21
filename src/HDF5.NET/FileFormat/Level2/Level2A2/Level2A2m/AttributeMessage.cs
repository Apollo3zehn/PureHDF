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

            if (this.Version == 1)
                reader.ReadByte();
            else
                this.Flags = (AttributeMessageFlags)reader.ReadByte();

            // name size
            this.NameSize = reader.ReadUInt16();

            // datatype size
            this.DatatypeSize = reader.ReadUInt16();

            // dataspace size
            this.DataspaceSize = reader.ReadUInt16();

            // name character set encoding
            if (this.Version == 3)
                this.NameCharacterSetEncoding = (CharacterSetEncoding)reader.ReadByte();
            
            // name
            if (this.Version == 1)
                this.Name = H5Utils.ReadNullTerminatedString(reader, pad: true, this.NameCharacterSetEncoding);
            else
                this.Name = H5Utils.ReadNullTerminatedString(reader, pad: false, this.NameCharacterSetEncoding);

            // datatype
#error: why is there a difference between "datatype_total" and this.DatatypeSize? (also for dataspace)
            var datatype_before = reader.BaseStream.Position;
            this.Datatype = new DatatypeMessage(reader);
            var datatype_after = reader.BaseStream.Position;

            if (this.Version == 1)
            {
                var datatype_total = datatype_after - datatype_before;
                var paddedSize = (int)(Math.Ceiling(this.DatatypeSize / 8.0) * 8);
                var remainingSize = paddedSize - datatype_total;
                this.Reader.BaseStream.Seek(remainingSize, SeekOrigin.Current);
            }

            // dataspace 
            var dataspace_before = reader.BaseStream.Position;
            this.Dataspace = new DataspaceMessage(reader, superblock);
            var dataspace_after = reader.BaseStream.Position;

            if (this.Version == 1)
            {
                var dataspace_total = dataspace_after - dataspace_before;
                var paddedSize = (int)(Math.Ceiling(this.DataspaceSize / 8.0) * 8);
                var remainingSize = paddedSize - dataspace_total;
                this.Reader.BaseStream.Seek(remainingSize, SeekOrigin.Current);
            }

            // data
#warning determine size correctly from datatype and dataspace
            this.Data = reader.ReadBytes(16);
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
