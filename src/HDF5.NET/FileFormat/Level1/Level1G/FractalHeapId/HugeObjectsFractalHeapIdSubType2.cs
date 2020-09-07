using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType2 : HugeObjectsFractalHeapIdSubType1
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType2(BinaryReader reader, Superblock superblock, BinaryReader localReader, FractalHeapHeader header) 
            : base(reader, superblock, localReader, header)
        {
            //
        }

        #endregion

        #region Methods

        public override T Read<T>(Func<BinaryReader, T> func, [AllowNull]ref IEnumerable<BTree2Record01> record01Cache)
        {
            throw new Exception("Filtered data is not yet supported.");
        }

        #endregion
    }
}
