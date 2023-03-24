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
            uint status = 0;

            var targetSize = parameters[2];

            while (status == 0)
            {
                var target = new byte[targetSize];
                status = Decompress(buffer.Span, target);

                if (status == 0)
                    targetSize += (uint)buffer.Length;

                else
                    return target.AsMemory(0, (int)status);
            }
            
            throw new Exception("This should never happen.");
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    };

    // https://github.com/h5py/h5py/blob/7e769ee3e229848e1fd74eb56382cb7b82c97ed0/lzf/lzf/lzf_d.c
    private static unsafe uint Decompress(Span<byte> input, Span<byte> output)
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
                        return 0;

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
                        return 0;

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

            return (uint)(op - _op);
        }
    }
}