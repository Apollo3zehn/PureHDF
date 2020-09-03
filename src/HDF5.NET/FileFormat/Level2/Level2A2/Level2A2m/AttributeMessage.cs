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

        public AttributeMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            //var a = reader.ReadBytes(200);
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
                this.NameEncoding = (CharacterSetEncoding)reader.ReadByte();
            
            // name
            if (this.Version == 1)
#error: padding is implemented wrongly. Should pad zero, but pads 8
                this.Name = H5Utils.ReadNullTerminatedString(reader, pad: true, encoding: this.NameEncoding);
            else
                this.Name = H5Utils.ReadNullTerminatedString(reader, pad: false, encoding: this.NameEncoding);

            // datatype
            this.Datatype = new DatatypeMessage(reader);

            if (this.Version == 1)
            {
                var paddedSize = (int)(Math.Ceiling(this.DatatypeSize / 8.0) * 8);
                var remainingSize = paddedSize - this.DatatypeSize;
                reader.ReadBytes(remainingSize);
            }

            // dataspace 
            this.Dataspace = new DataspaceMessage(reader, superblock);

            if (this.Version == 1)
            {
                var paddedSize = (int)(Math.Ceiling(this.DataspaceSize / 8.0) * 8);
                var remainingSize = paddedSize - this.DataspaceSize;
                this.Reader.BaseStream.Seek(remainingSize, SeekOrigin.Current);
            }

            // data
            var totalLength = this.Dataspace.DimensionSizes.Aggregate((x, y) => x * y);
            totalLength *= this.Datatype.Size;

            this.Data = reader.ReadBytes((int)totalLength);
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
        public CharacterSetEncoding NameEncoding { get; set; }
        public string Name { get; set; }
        public DatatypeMessage Datatype { get; set; }
        public DataspaceMessage Dataspace { get; set; }
        public byte[] Data { get; set; }

        #endregion
    }
}
