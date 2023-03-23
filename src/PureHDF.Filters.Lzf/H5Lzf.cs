namespace PureHDF.Filters;

/// <summary>
/// Contains a function to enable support for the LZF filter.
/// </summary>
public static class H5Lzf
{
    /// <summary>
    /// Gets the filter function.
    /// </summary>
    public unsafe static FilterFunction FilterFunction { get; } = (flags, parameters, buffer) =>
    {
        /* We're decompressing */
        if (flags.HasFlag(H5FilterFlags.Decompress))
        {
            var target = new byte[parameters[2]];
            Decompress(buffer.Span, target);

            return target;
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    };

    // https://github.com/h5py/h5py/blob/7e769ee3e229848e1fd74eb56382cb7b82c97ed0/lzf/lzf/lzf_d.c
    private static unsafe void Decompress(Span<byte> input, Span<byte> output)
    {
        fixed (byte* _ip = input, _op = output)
        {
            byte* ip = _ip;
            byte* op = _op;
            byte* in_end  = ip + input.Length;
            byte* out_end = op + output.Length;

            do
            {
                uint ctrl = *ip++;

                if (ctrl < (1 << 5)) /* literal run */
                {
                    ctrl++;

                    if (op + ctrl > out_end)
                        throw new Exception("E2BIG");

                    do
                    {
                        *op++ = *ip++;
                    }
                    while (--ctrl != 0);
                }

                else /* back reference */
                {
                    uint len = ctrl >> 5;

                    byte* _ref = op - ((ctrl & 0x1f) << 8) - 1;

                    if (len == 7)
                        len += *ip++;

                    _ref -= *ip++;

                    if (op + len + 2 > out_end)
                        throw new Exception("E2BIG");

                    if (_ref < _op)
                        throw new Exception("EINVAL");

                    *op++ = *_ref++;
                    *op++ = *_ref++;

                    do
                        *op++ = *_ref++;
                    while (--len != 0);
                }
            }
            while (ip < in_end);
        }
    }
}