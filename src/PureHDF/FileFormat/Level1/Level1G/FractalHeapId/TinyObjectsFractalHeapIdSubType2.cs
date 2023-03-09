namespace PureHDF
{
    internal class TinyObjectsFractalHeapIdSubType2 : TinyObjectsFractalHeapIdSubType1
    {
        #region Fields

        private readonly byte _firstByte;
        private readonly byte _extendedLength;

        #endregion

        #region Constructors

        public TinyObjectsFractalHeapIdSubType2(H5DriverBase localDriver, byte firstByte)
            : base(localDriver, firstByte)
        {
            _firstByte = firstByte;

            // extendedLength
            _extendedLength = localDriver.ReadByte();

            // data
            Data = localDriver.ReadBytes(Length);
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
