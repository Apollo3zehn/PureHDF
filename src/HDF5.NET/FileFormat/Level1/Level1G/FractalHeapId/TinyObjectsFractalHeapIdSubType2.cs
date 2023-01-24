namespace HDF5.NET
{
    internal class TinyObjectsFractalHeapIdSubType2 : TinyObjectsFractalHeapIdSubType1
    {
        #region Fields

        private byte _firstByte;
        private byte _extendedLength;

        #endregion

        #region Constructors

        public TinyObjectsFractalHeapIdSubType2(H5BaseReader localReader, byte firstByte) 
            : base(localReader, firstByte)
        {
            _firstByte = firstByte;

            // extendedLength
            _extendedLength = localReader.ReadByte();

            // data
            Data = localReader.ReadBytes(Length);
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
