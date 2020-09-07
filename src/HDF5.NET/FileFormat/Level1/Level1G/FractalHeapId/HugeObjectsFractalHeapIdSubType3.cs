using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType3 : FractalHeapId
    {
        #region Fields

        private BinaryReader _reader;

        #endregion

        #region Constructors

        public HugeObjectsFractalHeapIdSubType3(BinaryReader reader, Superblock superblock, BinaryReader localReader) : base(reader)
        {
            _reader = reader;

            // address
            this.Address = superblock.ReadOffset(localReader);

            // length
            this.Length = superblock.ReadLength(localReader);
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Length { get; set; }

        #endregion

        #region Method

        public override T Read<T>(Func<BinaryReader, T> func, [AllowNull] ref IEnumerable<BTree2Record01> record01Cache)
        {
            _reader.BaseStream.Seek((long)this.Address, SeekOrigin.Begin);
            return func(_reader);
        }

        #endregion
    }
}
