using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace HDF5.NET
{
    public class TinyObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Fields

        private byte _firstByte;

        #endregion

        #region Constructors

        public TinyObjectsFractalHeapIdSubType1(H5BinaryReader localReader, byte firstByte)
        {
            _firstByte = firstByte;

            // data
            this.Data = localReader.ReadBytes(this.Length);
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

        public override T Read<T>(Func<H5BinaryReader, T> func, [AllowNull] ref IEnumerable<BTree2Record01> record01Cache)
        {
            using var reader = new H5BinaryReader(new MemoryStream(this.Data));
            return func.Invoke(reader);
        }

        #endregion
    }
}
