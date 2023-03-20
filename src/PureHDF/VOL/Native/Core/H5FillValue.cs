using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal class H5FillValue : IH5FillValue
{
    #region Fields

    private readonly FillValueMessage _fillValue;

    #endregion

    #region Constructors

    internal H5FillValue(FillValueMessage fillValue)
    {
        _fillValue = fillValue;
    }

    #endregion

    #region Properties

    public byte[]? Value => _fillValue.Value?.ToArray();

    public T? GetValue<T>() where T : unmanaged => _fillValue.Value is null
        ? default
        : MemoryMarshal.Cast<byte, T>(_fillValue.Value.AsSpan())[0];

    #endregion
}