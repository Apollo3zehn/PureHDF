using System;
using System.IO;

namespace HDF5.NET
{
    public class TinyObjectsFractalHeapIdSubType2 : FractalHeapId
    {
        #region Fields

        private byte _versionTypeLength;
        private byte _extendedLength;

        #endregion

        #region Constructors

        public TinyObjectsFractalHeapIdSubType2(BinaryReader reader) : base(reader)
        {
            // version, type and length
            _versionTypeLength = reader.ReadByte();

            // extended length
            _extendedLength = reader.ReadByte();

            // data
            this.Data = reader.ReadBytes(this.Length);
        }

        #endregion

        #region Properties

        public ushort Length // bits 0-3
        {
            get
            {
                var highByte = (byte)((_versionTypeLength & 0x07) >> 0);

                return (ushort)(_extendedLength + (highByte << 8) + 1);         // take
            }
            set
            {
                if (!(1 <= value && value <= 4096)) // value must be <= 2^12
                    throw new FormatException("The length of an extended tiny object must be in the range of 1..4096");

                var actualValue = value - 1;
                var highByte = (byte)(value >> 8);
                var lowByte = (byte)(value & 0xFF);

                _versionTypeLength &= 0xF8;                                     // clear
                _versionTypeLength |= (byte)(highByte << 0);                    // set

                _extendedLength = lowByte;
            }
        }

        public byte[] Data { get; set; }

        protected override FractalHeapIdType ExpectedType => FractalHeapIdType.Tiny;

        #endregion
    }
}
