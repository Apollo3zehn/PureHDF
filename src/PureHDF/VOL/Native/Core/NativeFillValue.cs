using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal class NativeFillValue : IH5FillValue
{
    #region Fields

    private readonly FillValueMessage _fillValue;

    #endregion

    #region Constructors

    internal NativeFillValue(FillValueMessage fillValue)
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