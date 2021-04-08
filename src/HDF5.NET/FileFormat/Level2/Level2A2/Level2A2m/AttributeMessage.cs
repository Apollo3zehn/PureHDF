using System;
using System.IO;

namespace HDF5.NET
{
    internal class AttributeMessage : Message
    {
        #region Fields

        private byte _version;
        private CharacterSetEncoding _nameEncoding;
        private H5Context _context;

        #endregion

        #region Constructors

        public AttributeMessage(H5Context context, ObjectHeader objectHeader) : base(context.Reader)
        {
            _context = context;

            // version
            this.Version = context.Reader.ReadByte();

            if (this.Version == 1)
                context.Reader.ReadByte();
            else
                this.Flags = (AttributeMessageFlags)context.Reader.ReadByte();

            // name size
            var nameSize = context.Reader.ReadUInt16();

            // datatype size
            var datatypeSize = context.Reader.ReadUInt16();

            // dataspace size
            var dataspaceSize = context.Reader.ReadUInt16();

            // name character set encoding
            if (this.Version == 3)
                _nameEncoding = (CharacterSetEncoding)context.Reader.ReadByte();
            
            // name
            if (this.Version == 1)
                this.Name = H5Utils.ReadNullTerminatedString(context.Reader, pad: true, encoding: _nameEncoding);
            else
                this.Name = H5Utils.ReadNullTerminatedString(context.Reader, pad: false, encoding: _nameEncoding);

            // datatype
            var flags1 = this.Flags.HasFlag(AttributeMessageFlags.SharedDatatype)
                ? MessageFlags.Shared
                : MessageFlags.NoFlags;

            this.Datatype = objectHeader.DecodeMessage(flags1, 
                () => new DatatypeMessage(context.Reader));

            if (this.Version == 1)
            {
                var paddedSize = (int)(Math.Ceiling(datatypeSize / 8.0) * 8);
                var remainingSize = paddedSize - datatypeSize;
                context.Reader.ReadBytes(remainingSize);
            }

            // dataspace 
            var flags2 = this.Flags.HasFlag(AttributeMessageFlags.SharedDataspace)
                ? MessageFlags.Shared
                : MessageFlags.NoFlags;

            this.Dataspace = objectHeader.DecodeMessage(flags2,
                () => new DataspaceMessage(context.Reader, context.Superblock));

            if (this.Version == 1)
            {
                var paddedSize = (int)(Math.Ceiling(dataspaceSize / 8.0) * 8);
                var remainingSize = paddedSize - dataspaceSize;
                this.Reader.Seek(remainingSize, SeekOrigin.Current);
            }

            // data
            var byteSize = H5Utils.CalculateSize(this.Dataspace.DimensionSizes, this.Dataspace.Type) * this.Datatype.Size;
            this.Data = context.Reader.ReadBytes((int)byteSize);
        }

        private DatatypeMessage? ReadSharedMessage(ObjectHeader objectHeader, SharedMessage sharedMessage)
        {
            throw new NotImplementedException();
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

        public string Name { get; set; }

        public DatatypeMessage Datatype { get; set; }

        public DataspaceMessage Dataspace { get; set; }

        public byte[] Data { get; set; }

        #endregion
    }
}
