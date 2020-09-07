using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Fields

        private BinaryReader _reader;
        private Superblock _superblock;
        private FractalHeapHeader _heapHeader;

        #endregion

        #region Constructors

        internal HugeObjectsFractalHeapIdSubType1(BinaryReader reader, Superblock superblock, BinaryReader localReader, FractalHeapHeader header) : base(reader)
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

        public override T Read<T>(Func<BinaryReader, T> func, [AllowNull]ref IEnumerable<BTree2Record01> record01Cache)
        {
            // huge objects b-tree v2
            if (record01Cache == null)
            {
                _reader.BaseStream.Seek((long)_heapHeader.HugeObjectsBTree2Address, SeekOrigin.Begin);
                var hugeBtree2 = new BTree2Header<BTree2Record01>(_reader, _superblock);
                record01Cache = hugeBtree2.GetRecords();
            }

            var hugeRecord = record01Cache.FirstOrDefault(record => record.HugeObjectId == this.BTree2Key);
            _reader.BaseStream.Seek((long)hugeRecord.HugeObjectAddress, SeekOrigin.Begin);
            
            return func(_reader);
        }

        #endregion
    }
}
