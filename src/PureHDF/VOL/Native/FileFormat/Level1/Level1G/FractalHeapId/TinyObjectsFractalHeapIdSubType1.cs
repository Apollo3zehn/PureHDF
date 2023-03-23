using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal class TinyObjectsFractalHeapIdSubType1 : FractalHeapId
{
    #region Fields

    private readonly byte _firstByte;

    #endregion

    #region Constructors

    public TinyObjectsFractalHeapIdSubType1(H5DriverBase localDriver, byte firstByte)
    {
        _firstByte = firstByte;

        // data
        Data = localDriver.ReadBytes(Length);
    }

    #endregion

    #region Properties

    public byte Length // bits 0-3
    {
        get
        {
            return (byte)(((_firstByte & 0x0F) >> 0) + 1);          // take
        }
    }

    public byte[] Data { get; set; }

    #endregion

    #region Methods

    public override T Read<T>(Func<H5DriverBase, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        using var driver = new H5StreamDriver(new MemoryStream(Data), leaveOpen: false);
        return func.Invoke(driver);
    }

    #endregion
}