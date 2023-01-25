﻿using System.Diagnostics.CodeAnalysis;

namespace PureHDF
{
    internal class HugeObjectsFractalHeapIdSubType2 : HugeObjectsFractalHeapIdSubType1
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType2(H5Context context, H5BaseReader localReader, FractalHeapHeader header)
            : base(context, localReader, header)
        {
            //
        }

        #endregion

        #region Methods

        public override T Read<T>(Func<H5BaseReader, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
        {
            throw new Exception("Filtered data is not yet supported.");
        }

        #endregion
    }
}
