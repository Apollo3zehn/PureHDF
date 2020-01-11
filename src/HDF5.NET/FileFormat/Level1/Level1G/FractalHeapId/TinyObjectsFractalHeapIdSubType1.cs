using System;
using System.IO;

namespace HDF5.NET
{
    public class TinyObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Constructors

        public TinyObjectsFractalHeapIdSubType1(BinaryReader reader) : base(reader)
        {
            // data
            this.Data = reader.ReadBytes(this.Length);
        }

        #endregion

        #region Properties

        public byte Length // bits 0-3
        {
            get
            {
                return (byte)(((this.FirstByte & 0x07) >> 0) + 1);          // take
            }
            set
            {
                if (!(1 <= value && value <= 8)) // value must be <= 2^3
                    throw new FormatException("The length of an extended tiny object must be in the range of 1..8");

                var actualValue = value - 1;

                this.FirstByte &= 0xF8;                                     // clear
                this.FirstByte |= (byte)(actualValue << 0);                 // set
            }
        }

        public byte[] Data { get; set; }

        protected override FractalHeapIdType ExpectedType => FractalHeapIdType.Tiny;

        #endregion
    }
}
