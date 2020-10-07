using System;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public class AttributeMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public AttributeMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            //var a = reader.ReadBytes(200);
            // version
            this.Version = reader.ReadByte();

            if (this.Version == 1)
                reader.ReadByte();
            else
                this.Flags = (AttributeMessageFlags)reader.ReadByte();

            // name size
            var nameSize = reader.ReadUInt16();

            // datatype size
            var datatypeSize = reader.ReadUInt16();

            // dataspace size
            var dataspaceSize = reader.ReadUInt16();

            // name character set encoding
            if (this.Version == 3)
                this.NameEncoding = (CharacterSetEncoding)reader.ReadByte();
            
            // name
            if (this.Version == 1)
                this.Name = H5Utils.ReadNullTerminatedString(reader, pad: true, encoding: this.NameEncoding);
            else
                this.Name = H5Utils.ReadNullTerminatedString(reader, pad: false, encoding: this.NameEncoding);

            // datatype
            this.Datatype = new DatatypeMessage(reader);

            if (this.Version == 1)
            {
                var paddedSize = (int)(Math.Ceiling(datatypeSize / 8.0) * 8);
                var remainingSize = paddedSize - datatypeSize;
                reader.ReadBytes(remainingSize);
            }

            // dataspace 
            this.Dataspace = new DataspaceMessage(reader, superblock);

            if (this.Version == 1)
            {
                var paddedSize = (int)(Math.Ceiling(dataspaceSize / 8.0) * 8);
                var remainingSize = paddedSize - dataspaceSize;
                this.Reader.Seek(remainingSize, SeekOrigin.Current);
            }

            // data
            var byteSize = H5Utils.CalculateSize(this.Dataspace.DimensionSizes, this.Dataspace.Type) * this.Datatype.Size;
            this.Data = reader.ReadBytes((int)byteSize);
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
        public CharacterSetEncoding NameEncoding { get; set; }
        public string Name { get; set; }
        public DatatypeMessage Datatype { get; set; }
        public DataspaceMessage Dataspace { get; set; }
        public byte[] Data { get; set; }

        #endregion
    }
}
