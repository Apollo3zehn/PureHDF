using System.IO;

namespace HDF5.NET
{
    public class TinyObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Fields

        private byte _firstByte;

        #endregion

        #region Constructors

        public TinyObjectsFractalHeapIdSubType1(BinaryReader reader, byte firstByte) : base(reader)
        {
            _firstByte = firstByte;

            // data
            this.Data = reader.ReadBytes(this.Length);
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
    }
}
