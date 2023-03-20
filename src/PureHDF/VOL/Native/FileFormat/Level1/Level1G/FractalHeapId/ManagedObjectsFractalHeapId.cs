using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal class ManagedObjectsFractalHeapId : FractalHeapId
{
    #region Fields

    private readonly H5DriverBase _driver;
    private readonly FractalHeapHeader _header;

    #endregion

    #region Constructors

    public ManagedObjectsFractalHeapId(H5DriverBase driver, H5DriverBase localDriver, FractalHeapHeader header, ulong offsetByteCount, ulong lengthByteCount)
    {
        _driver = driver;
        _header = header;

        Offset = Utils.ReadUlong(localDriver, offsetByteCount);
        Length = Utils.ReadUlong(localDriver, lengthByteCount);
    }

    #endregion

    #region Properties

    public ulong Offset { get; set; }
    public ulong Length { get; set; }

    #endregion

    #region Methods

    public override T Read<T>(Func<H5DriverBase, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        var address = _header.GetAddress(this);

        _driver.Seek((long)address, SeekOrigin.Begin);
        return func(_driver);
    }

    #endregion
}