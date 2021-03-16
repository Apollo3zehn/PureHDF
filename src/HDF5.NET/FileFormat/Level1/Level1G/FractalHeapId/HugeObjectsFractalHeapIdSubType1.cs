using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    internal class HugeObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Fields

        private H5BinaryReader _reader;
        private Superblock _superblock;
        private FractalHeapHeader _heapHeader;

        #endregion

        #region Constructors

        internal HugeObjectsFractalHeapIdSubType1(H5BinaryReader reader, Superblock superblock, H5BinaryReader localReader, FractalHeapHeader header)
        {
            _reader = reader;
            _superblock = superblock;
            _heapHeader = header;

            // BTree2 key
            this.BTree2Key = H5Utils.ReadUlong(localReader, header.HugeIdsSize);
        }

        #endregion

        #region Properties

        public ulong BTree2Key { get; set; }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BinaryReader, T> func, [AllowNull]ref List<BTree2Record01> record01Cache)
        {
            // huge objects b-tree v2
            if (record01Cache is null)
            {
                _reader.Seek((long)_heapHeader.HugeObjectsBTree2Address, SeekOrigin.Begin);
                var hugeBtree2 = new BTree2Header<BTree2Record01>(_reader, _superblock, this.DecodeRecord01);
                record01Cache = hugeBtree2.EnumerateRecords().ToList();
            }

            var hugeRecord = record01Cache.FirstOrDefault(record => record.HugeObjectId == this.BTree2Key);
            _reader.Seek((long)hugeRecord.HugeObjectAddress, SeekOrigin.Begin);
            
            return func(_reader);
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record01 DecodeRecord01() => new BTree2Record01(_reader, _superblock);

        #endregion
    }
}
