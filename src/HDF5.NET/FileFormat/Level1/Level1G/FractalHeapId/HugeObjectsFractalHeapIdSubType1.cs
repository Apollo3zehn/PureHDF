using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    internal class HugeObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Fields

        private H5Context _context;
        private readonly FractalHeapHeader _heapHeader;

        #endregion

        #region Constructors

        internal HugeObjectsFractalHeapIdSubType1(H5Context context, H5BaseReader localReader, FractalHeapHeader header)
        {
            _context = context;
            _heapHeader = header;

            // BTree2 key
            BTree2Key = H5Utils.ReadUlong(localReader, header.HugeIdsSize);
        }

        #endregion

        #region Properties

        public ulong BTree2Key { get; set; }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BaseReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            var reader = _context.Reader;

            // huge objects b-tree v2
            if (record01Cache is null)
            {
                reader.Seek((long)_heapHeader.HugeObjectsBTree2Address, SeekOrigin.Begin);
                var hugeBtree2 = new BTree2Header<BTree2Record01>(_context, DecodeRecord01);
                record01Cache = hugeBtree2.EnumerateRecords().ToList();
            }

            var hugeRecord = record01Cache.FirstOrDefault(record => record.HugeObjectId == BTree2Key);
            reader.Seek((long)hugeRecord.HugeObjectAddress, SeekOrigin.Begin);

            return func(reader);
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record01 DecodeRecord01() => new BTree2Record01(_context);

        #endregion
    }
}
