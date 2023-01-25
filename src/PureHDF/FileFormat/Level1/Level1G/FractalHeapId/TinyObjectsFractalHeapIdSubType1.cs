using System.Diagnostics.CodeAnalysis;

namespace PureHDF
{
    internal class TinyObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Fields

        private readonly byte _firstByte;

        #endregion

        #region Constructors

        public TinyObjectsFractalHeapIdSubType1(H5BaseReader localReader, byte firstByte)
        {
            _firstByte = firstByte;

            // data
            Data = localReader.ReadBytes(Length);
        }

        #endregion

        #region Properties

        public byte Length // bits 0-3
        {
            get
            {
                return (byte)(((_firstByte & 0x0F) >> 0) + 1);          // take
            }
        }

        public byte[] Data { get; set; }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BaseReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            using var reader = new H5StreamReader(new MemoryStream(Data), leaveOpen: false);
            return func.Invoke(reader);
        }

        #endregion
    }
}
