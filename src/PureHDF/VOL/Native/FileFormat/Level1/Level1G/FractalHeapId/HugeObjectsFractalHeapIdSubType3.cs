using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal class HugeObjectsFractalHeapIdSubType3 : FractalHeapId
{
    #region Fields

    private readonly H5DriverBase _driver;

    #endregion

    #region Constructors

    public HugeObjectsFractalHeapIdSubType3(H5Context context, H5DriverBase localDriver)
    {
        var (driver, superblock) = context;
        _driver = driver;

        // address
        Address = superblock.ReadOffset(localDriver);

        // length
        Length = superblock.ReadLength(localDriver);
    }

    #endregion

    #region Properties

    public ulong Address { get; set; }
    public ulong Length { get; set; }

    #endregion

    #region Method

    public override T Read<T>(Func<H5DriverBase, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        _driver.Seek((long)Address, SeekOrigin.Begin);
        return func(_driver);
    }

    #endregion
}