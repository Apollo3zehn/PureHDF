using System;
using System.IO;

namespace HDF5.NET
{
    public class FillValueMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public FillValueMessage(BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            switch (this.Version)
            {
                case 1:

                    this.SpaceAllocationTime = (SpaceAllocationTime)reader.ReadByte();
                    this.FillValueWriteTime = (FillValueWriteTime)reader.ReadByte();
                    this.IsFillValueDefined = reader.ReadByte() == 1;
                    this.Size = reader.ReadUInt32();
                    this.FillValue = reader.ReadBytes((int)this.Size);

                    break;

                case 2:

                    this.SpaceAllocationTime = (SpaceAllocationTime)reader.ReadByte();
                    this.FillValueWriteTime = (FillValueWriteTime)reader.ReadByte();
                    this.IsFillValueDefined = reader.ReadByte() == 1;

                    if (this.IsFillValueDefined)
                    {
                        this.Size = reader.ReadUInt32();
                        this.FillValue = reader.ReadBytes((int)this.Size);
                    }

                    break;

                case 3:

                    this.SpaceAllocationTime = (SpaceAllocationTime)((reader.ReadByte() & 0x03) >> 0);  // take only bits 0 and 1
                    this.FillValueWriteTime = (FillValueWriteTime)((reader.ReadByte() & 0x0B) >> 2);    // take only bits 2 and 3
                    this.IsFillValueUndefined = (reader.ReadByte() & (1 << 4)) > 0;                     // take only bit 4
                    this.IsFillValueDefined = (reader.ReadByte() & (1 << 5)) > 0;                       // take only bit 5

#warning make sure that both bool values cannot be true at the same time

                    if (this.IsFillValueDefined)
                    {
                        this.Size = reader.ReadUInt32();
                        this.FillValue = reader.ReadBytes((int)this.Size);
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
                if (!(1 <= _version && _version <= 3))
                    throw new FormatException($"Only version 1-3 instances of type {nameof(FillValueMessage)} are supported.");

                _version = value;
            }
        }

        public SpaceAllocationTime SpaceAllocationTime { get; set; }
        public FillValueWriteTime FillValueWriteTime { get; set; }
        public bool IsFillValueUndefined { get; set; }
        public bool IsFillValueDefined { get; set; }
        public uint Size { get; set; }
        public byte[] FillValue { get; set; }

        #endregion
    }
}
