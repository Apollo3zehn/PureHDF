namespace HDF5.NET
{
    internal class DataBlockPage<T>
    {
        #region Constructors

        public DataBlockPage(H5BaseReader reader, ulong elementCount, Func<H5BaseReader, T> decode)
        {
            // elements
            Elements = Enumerable
                .Range(0, (int)elementCount)
                .Select(i => decode(reader))
                .ToArray();

            // checksum
            Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public T[] Elements { get; }

        public ulong Checksum { get; }

        #endregion
    }
}
