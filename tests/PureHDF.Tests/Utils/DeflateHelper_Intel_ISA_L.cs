using ISA_L.PInvoke;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF.Tests
{
    public static class DeflateHelper_Intel_ISA_L
    {
        private static readonly int _state_length = Unsafe.SizeOf<inflate_state>();

        private static readonly ThreadLocal<IntPtr> _state_ptr = new(
            valueFactory: CreateState,
            trackAllValues: false);

        public static unsafe Memory<byte> FilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            /* We're decompressing */
            if (flags.HasFlag(H5FilterFlags.Decompress))
            {
                var state = new Span<inflate_state>(_state_ptr.Value.ToPointer(), _state_length);

                ISAL.isal_inflate_reset(_state_ptr.Value);

                buffer = buffer[2..]; // skip ZLIB header to get only the DEFLATE stream

                var length = 0;
                var inflated = new byte[buffer.Length /* minimum size to expect */];
                var sourceBuffer = buffer.Span;
                var targetBuffer = inflated.AsSpan();

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
                                tmp.CopyTo(inflated, 0);
                                targetBuffer = inflated.AsSpan(start: tmp.Length);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                return inflated
                    .AsMemory(0, length);
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
}
