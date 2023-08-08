using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ISA_L.PInvoke;

namespace PureHDF.Filters;

/// <summary>
/// Hardware accelerated Deflate filter based on Intel ISA-L.
/// </summary>
public class DeflateISALFilter : IH5Filter
{
    private static readonly int _state_length = Unsafe.SizeOf<inflate_state>();

    private static readonly ThreadLocal<IntPtr> _state_ptr = new(
        valueFactory: CreateState,
        trackAllValues: false);
        
    /// <inheritdoc />
    public H5FilterID Id => H5FilterID.Deflate;

    /// <inheritdoc />
    public string Name => "deflate";

    /// <inheritdoc />
    public unsafe Memory<byte> Filter(FilterInfo info)
    {
        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            var buffer = info.SourceBuffer;
            var state = new Span<inflate_state>(_state_ptr.Value.ToPointer(), _state_length);

            ISAL.isal_inflate_reset(_state_ptr.Value);

            buffer = buffer.Slice(2); // skip ZLIB header to get only the DEFLATE stream

            var length = 0;

            var inflated = info.FinalBuffer.Equals(default)
                ? new byte[info.ChunkSize]
                : info.FinalBuffer;

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
                            inflated = new byte[tmp.Length * 2];
                            tmp.CopyTo(inflated);
                            targetBuffer = inflated.Span.Slice(tmp.Length);
                        }
                        
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return inflated.Slice(0, length);
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        throw new NotImplementedException();
    }

    private static unsafe IntPtr CreateState()
    {
        var ptr = Marshal.AllocHGlobal(Unsafe.SizeOf<inflate_state>());
        new Span<byte>(ptr.ToPointer(), _state_length).Clear();

        return ptr;
    }
}