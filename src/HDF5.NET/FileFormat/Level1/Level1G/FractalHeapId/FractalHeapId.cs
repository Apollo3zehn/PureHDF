using System;
using System.IO;

namespace HDF5.NET
{
    public abstract class FractalHeapId : FileBlock
    {
        #region Constructors

        public FractalHeapId(BinaryReader reader) : base(reader)
        {
            // first byte
            this.FirstByte = reader.ReadByte();
        }

        #endregion

        #region Properties

        protected abstract FractalHeapIdType ExpectedType { get; }

        public byte Version // bits 6-7
        {
            get
            {
                return (byte)((this.FirstByte & 0xB0) >> 6);                // take
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapId)} are supported.");

                this.FirstByte &= 0x3F;                                     // clear
                this.FirstByte |= (byte)(value << 6);                       // set
            }
        }

        public FractalHeapIdType Type // bits 4-5
        {
            get
            {
                return (FractalHeapIdType)((this.FirstByte & 0x30) >> 4);   // take
            }
            set
            {
                if (value != this.ExpectedType)
                    throw new FormatException($"The fractal heap ID type is invalid.");

                this.FirstByte &= 0xBF;                                     // clear
                this.FirstByte |= (byte)((byte)value << 4);                 // set
            }
        }

        protected byte FirstByte { get; set; }

        #endregion
    }
}
