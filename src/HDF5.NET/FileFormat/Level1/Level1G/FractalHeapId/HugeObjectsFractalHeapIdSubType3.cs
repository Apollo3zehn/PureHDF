using System.Diagnostics.CodeAnalysis;

namespace HDF5.NET
{
    internal class HugeObjectsFractalHeapIdSubType3 : FractalHeapId
    {
        #region Fields

        private H5BinaryReader _reader;

        #endregion

        #region Constructors

        public HugeObjectsFractalHeapIdSubType3(H5Context context, H5BinaryReader localReader)
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

        public override T Read<T>(Func<H5BinaryReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            _reader.Seek((long)Address, SeekOrigin.Begin);
            return func(_reader);
        }

        #endregion
    }
}
