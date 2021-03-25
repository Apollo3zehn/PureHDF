using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace HDF5.NET
{
    internal class HugeObjectsFractalHeapIdSubType2 : HugeObjectsFractalHeapIdSubType1
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType2(H5BinaryReader reader, Superblock superblock, H5BinaryReader localReader, FractalHeapHeader header) 
            : base(reader, superblock, localReader, header)
        {
            //
        }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BinaryReader, T> func, [AllowNull]ref List<BTree2Record01> record01Cache)
        {
            throw new Exception("Filtered data is not yet supported.");
        }

        #endregion
    }
}
