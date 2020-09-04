using System.IO;

namespace HDF5.NET
{
    public class TinyObjectsFractalHeapIdSubType2 : TinyObjectsFractalHeapIdSubType1
    {
        #region Fields

        private byte _firstByte;
        private byte _extendedLength;

        #endregion

        #region Constructors

        public TinyObjectsFractalHeapIdSubType2(BinaryReader reader, byte firstByte) 
            : base(reader, firstByte)
        {
            _firstByte = firstByte;

            // extendedLength
            _extendedLength = reader.ReadByte();

            // data
            this.Data = reader.ReadBytes(this.Length);
        }

        #endregion

        #region Properties

        public new ushort Length // bits 0-3
        {
            get
            {
                var highByte = (byte)((_firstByte & 0x0F) >> 0);
                return (ushort)(_extendedLength | (highByte << 8) + 1);         // take
            }
        }

        #endregion
    }
}
