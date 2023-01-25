﻿using System.Diagnostics.CodeAnalysis;

namespace PureHDF
{
    internal class ManagedObjectsFractalHeapId : FractalHeapId
    {
        #region Fields

        private readonly H5BaseReader _reader;
        private readonly FractalHeapHeader _header;

        #endregion

        #region Constructors

        public ManagedObjectsFractalHeapId(H5BaseReader reader, H5BaseReader localReader, FractalHeapHeader header, ulong offsetByteCount, ulong lengthByteCount)
        {
            _reader = reader;
            _header = header;

            Offset = H5Utils.ReadUlong(localReader, offsetByteCount);
            Length = H5Utils.ReadUlong(localReader, lengthByteCount);
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BaseReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            var address = _header.GetAddress(this);

            _reader.Seek((long)address, SeekOrigin.Begin);
            return func(_reader);
        }

        #endregion
    }
}
