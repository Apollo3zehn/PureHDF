using System.Diagnostics.CodeAnalysis;

namespace HDF5.NET
{
    internal class ManagedObjectsFractalHeapId : FractalHeapId
    {
        #region Fields

        private H5BinaryReader _reader;
        private FractalHeapHeader _header;

        #endregion

        #region Constructors

        public ManagedObjectsFractalHeapId(H5BinaryReader reader, H5BinaryReader localReader, FractalHeapHeader header, ulong offsetByteCount, ulong lengthByteCount)
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

        public override T Read<T>(Func<H5BinaryReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            var address = _header.GetAddress(this);

            _reader.Seek((long)address, SeekOrigin.Begin);
            return func(_reader);
        }

        #endregion
    }
}
