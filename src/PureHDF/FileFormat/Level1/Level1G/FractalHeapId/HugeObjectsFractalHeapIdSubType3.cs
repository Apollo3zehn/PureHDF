using System.Diagnostics.CodeAnalysis;

namespace PureHDF
{
    internal class HugeObjectsFractalHeapIdSubType3 : FractalHeapId
    {
        #region Fields

        private readonly H5BaseReader _reader;

        #endregion

        #region Constructors

        public HugeObjectsFractalHeapIdSubType3(H5Context context, H5BaseReader localReader)
        {
            var (reader, superblock) = context;
            _reader = reader;

            // address
            Address = superblock.ReadOffset(localReader);

            // length
            Length = superblock.ReadLength(localReader);
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Length { get; set; }

        #endregion

        #region Method

        public override T Read<T>(Func<H5BaseReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            _reader.Seek((long)Address, SeekOrigin.Begin);
            return func(_reader);
        }

        #endregion
    }
}
