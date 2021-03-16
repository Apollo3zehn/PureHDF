using System;

namespace HDF5.NET
{
    // https://support.hdfgroup.org/HDF5/doc_resource/H5Fill_Behavior.html
    internal class FillValueMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public FillValueMessage(H5BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            switch (this.Version)
            {
                case 1:

                    this.AllocationTime = (SpaceAllocationTime)reader.ReadByte();
                    this.FillTime = (FillValueWriteTime)reader.ReadByte();
                    this.IsDefined = reader.ReadByte() == 1;
                    this.Size = reader.ReadUInt32();
                    this.Value = reader.ReadBytes((int)this.Size);

                    break;

                case 2:

                    this.AllocationTime = (SpaceAllocationTime)reader.ReadByte();
                    this.FillTime = (FillValueWriteTime)reader.ReadByte();
                    this.IsDefined = reader.ReadByte() == 1;

                    if (this.IsDefined)
                    {
                        this.Size = reader.ReadUInt32();
                        this.Value = reader.ReadBytes((int)this.Size);
                    }

                    break;

                case 3:

                    var flags = reader.ReadByte();
                    this.AllocationTime = (SpaceAllocationTime)((flags & 0x03) >> 0);   // take only bits 0 and 1
                    this.FillTime = (FillValueWriteTime)((flags & 0x0C) >> 2);          // take only bits 2 and 3
                    this.IsDefined = (flags & (1 << 5)) > 0;                            // take only bit 5

                    if (this.IsDefined)
                    {
                        this.Size = reader.ReadUInt32();
                        this.Value = reader.ReadBytes((int)this.Size);
                    }

                    break;

                default:
                    break;
            }
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
                    throw new FormatException($"Only version 1-3 instances of type {nameof(FillValueMessage)} are supported.");

                _version = value;
            }
        }

        public SpaceAllocationTime AllocationTime { get; set; }
        public FillValueWriteTime FillTime { get; set; }
        public bool IsDefined { get; set; }
        public uint Size { get; set; }
        public byte[] Value { get; set; }

        #endregion
    }
}
