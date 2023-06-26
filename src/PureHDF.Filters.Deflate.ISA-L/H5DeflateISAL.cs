using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ISA_L.PInvoke;

namespace PureHDF.Filters;

/// <summary>
/// Contains a function to enable support for the hardware accelerated Deflate filter based on Intel ISA-L.
/// </summary>
public static class H5DeflateISAL
{
    private static readonly int _state_length = Unsafe.SizeOf<inflate_state>();

    private static readonly ThreadLocal<IntPtr> _state_ptr = new(
        valueFactory: CreateState,
        trackAllValues: false);

    /// <summary>
    /// The hardware accelerated Deflate filter function based on Intel ISA-L.
    /// </summary>
    /// <param name="info">The filter info.</param>
    public unsafe static Memory<byte> FilterFunction(FilterInfo info)
    {
        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            var buffer = info.Buffer;
            var state = new Span<inflate_state>(_state_ptr.Value.ToPointer(), _state_length);

            ISAL.isal_inflate_reset(_state_ptr.Value);

            buffer = buffer.Slice(2); // skip ZLIB header to get only the DEFLATE stream

            var length = 0;
            var minimumSize = Math.Max(buffer.Length, info.ChunkSize);
            var inflated = info.GetResultBuffer(minimumSize);
            var sourceBuffer = buffer.Span;
            var targetBuffer = inflated.Span;

            fixed (byte* ptrIn = sourceBuffer)
            {
                state[0].next_in = ptrIn;
                state[0].avail_in = (uint)sourceBuffer.Length;

                while (true)
                {
                    fixed (byte* ptrOut = targetBuffer)
                    {
                        state[0].next_out = ptrOut;
                        state[0].avail_out = (uint)targetBuffer.Length;

                        var status = ISAL.isal_inflate(_state_ptr.Value);

                        if (status != inflate_return_values.ISAL_DECOMP_OK)
                            throw new Exception($"Error encountered while decompressing: {status}.");

                        length += targetBuffer.Length - (int)state[0].avail_out;

                        if (state[0].block_state != isal_block_state.ISAL_BLOCK_FINISH && /* not done */
                            state[0].avail_out == 0 /* and work to do */)
                        {
                            // double array size
                            var tmp = inflated;
                            inflated = info.GetResultBuffer(tmp.Length * 2 /* minimum size */);
                            tmp.CopyTo(inflated);
                            targetBuffer = inflated[tmp.Length..].Span;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return inflated[..length];
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    }

    private static unsafe IntPtr CreateState()
    {
        var ptr = Marshal.AllocHGlobal(Unsafe.SizeOf<inflate_state>());
        new Span<byte>(ptr.ToPointer(), _state_length).Clear();

        return ptr;
    }
}