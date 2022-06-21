using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace HDF5.NET
{
    internal class HugeObjectsFractalHeapIdSubType4 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType4(H5BinaryReader reader, Superblock superblock, H5BinaryReader localReader)
        {
            // address
            Address = superblock.ReadOffset(localReader);

            // length
            Length = superblock.ReadLength(localReader);

            // filter mask
            FilterMask = localReader.ReadUInt32();

            // de-filtered size
            DeFilteredSize = superblock.ReadLength(localReader);
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Length { get; set; }
        public uint FilterMask { get; set; }
        public ulong DeFilteredSize { get; set; }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BinaryReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            throw new Exception("Filtered data is not yet supported.");
        }

        #endregion
    }
}
