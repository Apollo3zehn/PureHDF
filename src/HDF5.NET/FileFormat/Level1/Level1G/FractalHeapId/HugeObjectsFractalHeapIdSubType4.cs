using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType4 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType4(H5BinaryReader reader, Superblock superblock, H5BinaryReader localReader)
        {
            // address
            this.Address = superblock.ReadOffset(localReader);

            // length
            this.Length = superblock.ReadLength(localReader);

            // filter mask
            this.FilterMask = localReader.ReadUInt32();

            // de-filtered size
            this.DeFilteredSize = superblock.ReadLength(localReader);
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Length { get; set; }
        public uint FilterMask { get; set; }
        public ulong DeFilteredSize { get; set; }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BinaryReader, T> func, [AllowNull] ref IEnumerable<BTree2Record01> record01Cache)
        {
            throw new Exception("Filtered data is not yet supported.");
        }

        #endregion
    }
}
