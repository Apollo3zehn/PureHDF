using System;
using System.Linq;

namespace HDF5.NET
{
    public class DataBlockPage<T>
    {
        #region Constructors

        public DataBlockPage(H5BinaryReader reader, ulong elementCount, Func<H5BinaryReader, T> decode)
        {
            // elements
            this.Elements = Enumerable
                .Range(0, (int)elementCount)
                .Select(i => decode(reader))
                .ToArray();

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public T[] Elements { get; }

        public ulong Checksum { get; }

        #endregion
    }
}
