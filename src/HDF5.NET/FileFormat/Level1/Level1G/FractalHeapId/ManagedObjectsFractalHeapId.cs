using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace HDF5.NET
{
    public class ManagedObjectsFractalHeapId : FractalHeapId
    {
        #region Fields

        private BinaryReader _reader;
        private FractalHeapHeader _header;

        #endregion

        #region Constructors

        public ManagedObjectsFractalHeapId(BinaryReader reader, BinaryReader localReader, FractalHeapHeader header, ulong offsetByteCount, ulong lengthByteCount) 
            : base(reader)
        {
            _reader = reader;
            _header = header;

            this.Offset = H5Utils.ReadUlong(localReader, offsetByteCount);
            this.Length = H5Utils.ReadUlong(localReader, lengthByteCount);
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        #endregion

        #region Methods

        public override T Read<T>(Func<BinaryReader, T> func, [AllowNull] ref IEnumerable<BTree2Record01> record01Cache)
        {
            var address = _header.GetAddress(this);

            _reader.BaseStream.Seek((long)address, SeekOrigin.Begin);
            return func(_reader);
        }

        #endregion
    }
}
