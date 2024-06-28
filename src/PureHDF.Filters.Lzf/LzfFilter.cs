using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF.Filters;

/* Compiling: 
 * 1. Clone h5py 3.9.0:
 * 2. Change constants:
 *      - VERY_FAST 0
 *      - ULTRA_FAST 0
 *      - INIT_HTAB 1
 * 3. Run: gcc -g *.c \
 *    -o test \
 *    -lhdf5 \
 *    -L<Path to HDF5>/lib/ \
 *    -I<Path to HDF5>/include/
 *
 * Note that it might be necessary to copy all files
 * from lzf/lfz folder one level above into the lzf folder.
 */

/// <summary>
/// LZF filter.
/// </summary>
public class LzfFilter : IH5Filter
{
    /// <summary>
    /// The LZF filter identifier.
    /// </summary>
    public const ushort Id = 32000;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "lzf";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        uint targetSize;
        uint status = 0;

        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {

            if ((info.Parameters.Length >= 3) && (info.Parameters[2] != 0))
                targetSize = info.Parameters[2];   /* Precomputed buffer guess */

            else
                targetSize = (uint)info.ChunkSize;

            while (status == 0)
            {
                var buffer = GC
                    .AllocateUninitializedArray<byte>((int)targetSize)
                    .AsMemory();

                status = Decompress(info.Buffer.Span, buffer.Span);

                if (status == 0)
                    targetSize += (uint)info.Buffer.Length;

                else
                    return buffer.Slice(0, (int)status);
            }

            throw new Exception("This should never happen.");
        }

        /* We're compressing */
        else
        {
            targetSize = (uint)info.Buffer.Length;
            var buffer = new byte[(int)targetSize].AsMemory();

            status = Compress(info.Buffer.Span, buffer.Span);

            if (status == 0)
                throw new Exception("The LFZ compression failed.");

            else
                return buffer;
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(uint[] chunkDimensions, uint typeSize, Dictionary<string, object>? options)
    {
        // https://github.com/h5py/h5py/blob/3.9.0/lzf/lzf_filter.c#L122

        var parameters = new uint[3];

        parameters[0] = 4;
        parameters[1] = 0x0105;

        var chunkSize = typeSize * chunkDimensions
            .Aggregate(1U, (product, dimension) => product * dimension);

        parameters[2] = chunkSize;

        return parameters;
    }

    // https://github.com/h5py/h5py/blob/3.9.0/lzf/lzf/lzf_c.c
    // http://oldhome.schmorp.de/marc/liblzf.html
    private static unsafe uint Compress(Span<byte> input, Span<byte> output)
    {
        // using var htab_rented = MemoryPool<IntPtr>.Shared.Rent(1 << HLOG);
        // var htab = htab_rented.Memory.Span;
        // htab_rented.Memory.Span.Clear();

        var htabSize = sizeof(byte*) * (1 << HLOG);
        var htab_allocated = Marshal.AllocHGlobal(htabSize);
        new Span<byte>((void*)htab_allocated, htabSize).Clear();

        var htab = (byte**)htab_allocated;

        try
        {
            fixed (byte*
                _ip = input,
                _op = output)
            {
                byte** hslot;
                byte* ip = _ip;
                byte* op = _op;
                byte* in_end = ip + input.Length;
                byte* out_end = op + output.Length;
                byte* @ref;

                ulong off;
                uint hval;
                int lit;

                if (input.Length == 0 || output.Length == 0)
                    return 0;

                lit = 0; op++;

                hval = FRST(ip);
                while (ip < in_end - 2)
                {
                    hval = NEXT(hval, ip);
                    hslot = htab + IDX(hval);
                    @ref = *hslot; *hslot = ip;

                    if
                    (
                        true
                        && @ref < ip
                        && (off = (ulong)ip - (ulong)@ref - 1) < MAX_OFF
                        && ip + 4 < in_end
                        && @ref > _ip
                        // #if STRICT_ALIGN
                        //         && @ref[0] == ip[0]
                        //         && @ref[1] == ip[1]
                        //         && @ref[2] == ip[2]
                        // #else
                        && *(ushort*)@ref == *(ushort*)ip
                        && @ref[2] == ip[2]
                    // #endif
                    )
                    {
                        /* match found at *@ref++ */
                        uint len = 2;
                        uint maxlen = (uint)((ulong)in_end - (ulong)ip - len);
                        maxlen = maxlen > MAX_REF ? MAX_REF : maxlen;

                        if (op + 3 + 1 >= out_end) /* first a faster conservative test */
                            if (op - (lit != 0 ? 0 : 1) + 3 + 1 >= out_end) /* second the exact but rare test */
                                return 0;

                        op[-lit - 1] = unchecked((byte)(lit - 1)); /* stop run */
                        op -= lit != 0 ? 0 : 1; /* undo run if length is zero */

                        for (; ; )
                        {
                            if (maxlen > 16)
                            {
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;

                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;

                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;

                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                                len++; if (@ref[len] != ip[len]) break;
                            }

                            do
                                len++;
                            while (len < maxlen && @ref[len] == ip[len]);

                            break;
                        }

                        len -= 2; /* len is now #octets - 1 */
                        ip++;

                        if (len < 7)
                        {
                            *op++ = (byte)((off >> 8) + (len << 5));
                        }

                        else
                        {
                            *op++ = (byte)((off >> 8) + (7 << 5));
                            *op++ = (byte)(len - 7);
                        }

                        *op++ = (byte)off;
                        lit = 0; op++; /* start run */

                        ip += len + 1;

                        if (ip >= in_end - 2)
                            break;

                        ip -= len + 1;

                        do
                        {
                            hval = NEXT(hval, ip);
                            htab[(int)IDX(hval)] = ip;
                            ip++;
                        }
                        while (len-- != 0);
                    }

                    else
                    {
                        /* one more literal byte we must copy */
                        if (op >= out_end)
                            return 0;

                        lit++; *op++ = *ip++;

                        if (lit == MAX_LIT)
                        {
                            op[-lit - 1] = unchecked((byte)(lit - 1)); /* stop run */
                            lit = 0; op++; /* start run */
                        }
                    }
                }

                if (op + 3 > out_end) /* at most 3 bytes can be missing here */
                    return 0;

                while (ip < in_end)
                {
                    lit++; *op++ = *ip++;

                    if (lit == MAX_LIT)
                    {
                        op[-lit - 1] = unchecked((byte)(lit - 1)); /* stop run */
                        lit = 0; op++; /* start run */
                    }
                }

                op[-lit - 1] = unchecked((byte)(lit - 1)); /* end run */
                op -= lit != 0 ? 0 : 1; /* undo run if length is zero */

                return (uint)(op - _op);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(htab_allocated);
        }
    }

    private const int HLOG = 17;
    private const int HSIZE = 1 << (HLOG);

    private const int MAX_LIT = 1 << 5;
    private const int MAX_REF = (1 << 8) + (1 << 3);
    private const int MAX_OFF = 1 << 13;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint FRST(byte* p)
    {
        return unchecked((uint)(((p[0]) << 8) | p[1]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint NEXT(uint v, byte* p)
    {
        return ((v) << 8) ^ p[2];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint IDX(uint h)
    {
        return (((h ^ (h << 5)) >> (3 * 8 - HLOG)) - h * 5) & (HSIZE - 1);
    }

    // https://github.com/h5py/h5py/blob/3.9.0/lzf/lzf/lzf_d.c
    private static unsafe uint Decompress(ReadOnlySpan<byte> input, Span<byte> output)
    {
        fixed (byte*
            _ip = input,
            _op = output)
        {
            byte* ip = _ip;
            byte* op = _op;
            byte* in_end = ip + input.Length;
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

                    byte* @ref = op - ((ctrl & 0x1f) << 8) - 1;

                    if (len == 7)
                        len += *ip++;

                    @ref -= *ip++;

                    if (op + len + 2 > out_end)
                        return 0;

                    if (@ref < _op)
                        throw new Exception("EINVAL");

                    *op++ = *@ref++;
                    *op++ = *@ref++;

                    do
                        *op++ = *@ref++;
                    while (--len != 0);
                }
            }
            while (ip < in_end);

            return (uint)(op - _op);
        }
    }
}