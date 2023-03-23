using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal class HugeObjectsFractalHeapIdSubType2 : HugeObjectsFractalHeapIdSubType1
{
    #region Constructors

    public HugeObjectsFractalHeapIdSubType2(NativeContext context, H5DriverBase localDriver, FractalHeapHeader header)
        : base(context, localDriver, header)
    {
        //
    }

    #endregion

    #region Methods

    public override T Read<T>(Func<H5DriverBase, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        throw new Exception("Filtered data is not yet supported.");
    }

    #endregion
}