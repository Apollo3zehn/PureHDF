using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ISA_L.PInvoke;

namespace PureHDF.Filters;

/// <summary>
/// Hardware accelerated Deflate filter based on Intel ISA-L.
/// </summary>
public class DeflateISALFilter : IH5Filter
{
    /// <summary>
    /// The compression level options key. The compression level must be in the range of 0..3 and the default is 0. See https://python-isal.readthedocs.io/en/stable/#differences-with-zlib-and-gzip-modules for more info about the compression levels.
    /// </summary>
    public const string COMPRESSION_LEVEL = "compression-level";

    /// <summary>
    /// The Deflate filter identifier.
    /// </summary>
    public const ushort Id = DeflateFilter.Id;

    private static readonly int _state_length = Unsafe.SizeOf<inflate_state>();
    private static readonly int _stream_length = Unsafe.SizeOf<isal_zstream>();

    private static readonly ThreadLocal<IntPtr> _state_ptr = new(
        valueFactory: CreateState,
        trackAllValues: false);
        
    private static readonly ThreadLocal<IntPtr> _stream_ptr = new(
        valueFactory: CreateStream,
        trackAllValues: false);

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "deflate";

    /// <inheritdoc />
    public unsafe Memory<byte> Filter(FilterInfo info)
    {
        var buffer = info.Buffer;

        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            var stateSpan = new Span<inflate_state>(_state_ptr.Value.ToPointer(), _state_length);
            ref inflate_state state = ref stateSpan[0];

            ISAL.isal_inflate_init(_state_ptr.Value);

            buffer = buffer.Slice(2); // skip ZLIB header to get only the DEFLATE stream

            var length = 0;
            var inflated = new byte[info.ChunkSize].AsMemory();

            var sourceBuffer = buffer.Span;
            var targetBuffer = inflated.Span;

            fixed (byte* ptrIn = sourceBuffer)
            {
                state.next_in = ptrIn;
                state.avail_in = (uint)sourceBuffer.Length;

                while (true)
                {
                    fixed (byte* ptrOut = targetBuffer)
                    {
                        state.next_out = ptrOut;
                        state.avail_out = (uint)targetBuffer.Length;

                        var status = ISAL.isal_inflate(_state_ptr.Value);

                        if (status != inflate_return_values.ISAL_DECOMP_OK)
                            throw new Exception($"Error encountered while decompressing: {status}.");

                        length += targetBuffer.Length - (int)state.avail_out;

                        if (state.block_state != isal_block_state.ISAL_BLOCK_FINISH &&
                            state.avail_out == 0)
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
            var streamSpan = new Span<isal_zstream>(_stream_ptr.Value.ToPointer(), _stream_length);
            ref isal_zstream stream = ref streamSpan[0];

            ISAL.isal_deflate_init(_stream_ptr.Value);

            var length = 0;
            var deflated = new byte[info.ChunkSize].AsMemory();

            var sourceBuffer = buffer.Span;
            var targetBuffer = deflated.Span;

            var level = info.Parameters[0];

            var levelBufferSize = level switch
            {
                0 => Constants.ISAL_DEF_LVL0_DEFAULT,
                1 => Constants.ISAL_DEF_LVL1_DEFAULT,
                2 => Constants.ISAL_DEF_LVL2_DEFAULT,
                3 => Constants.ISAL_DEF_LVL3_DEFAULT,
                _ => throw new Exception($"The level {level} is not supported.")
            };

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(levelBufferSize);
            var levelBuffer = memoryOwner.Memory.Span.Slice(0, levelBufferSize);

            fixed (byte* ptrIn = sourceBuffer, ptrLevel = levelBuffer)
            {
                stream.level = level;
                stream.level_buf = ptrLevel;
                stream.level_buf_size = (uint)levelBufferSize;

                stream.gzip_flag = Constants.IGZIP_ZLIB;

                stream.next_in = ptrIn;
                stream.avail_in = (uint)sourceBuffer.Length;
                stream.end_of_stream = 1;

                while (true)
                {
                    fixed (byte* ptrOut = targetBuffer)
                    {
                        stream.next_out = ptrOut;
                        stream.avail_out = (uint)targetBuffer.Length;

                        var status = ISAL.isal_deflate(_stream_ptr.Value);

                        if (status != inflate_return_values.ISAL_DECOMP_OK)
                            throw new Exception($"Error encountered while compressing: {status}.");

                        length += targetBuffer.Length - (int)stream.avail_out;

                        if (stream.avail_in > 0 && stream.avail_out == 0)
                        {
                            // double array size
                            var tmp = deflated;
                            deflated = new byte[tmp.Length * 2];
                            tmp.CopyTo(deflated);
                            targetBuffer = deflated.Span.Slice(tmp.Length);
                        }
                        
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return deflated.Slice(0, length);
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(uint[] chunkDimensions, uint typeSize, Dictionary<string, object>? options)
    {
        var value = GetCompressionLevelValue(options);

        if (value < 0 || value > 3)
            throw new Exception("The compression level must be in the range of 0..3.");

        return new uint[] { unchecked((uint)value) };
    }

    private static unsafe IntPtr CreateState()
    {
        var ptr = Marshal.AllocHGlobal(Unsafe.SizeOf<inflate_state>());
        new Span<byte>(ptr.ToPointer(), _state_length).Clear();

        return ptr;
    }

    private static unsafe IntPtr CreateStream()
    {
        var ptr = Marshal.AllocHGlobal(Unsafe.SizeOf<isal_zstream>());
        new Span<byte>(ptr.ToPointer(), _stream_length).Clear();

        return ptr;
    }

    private static int GetCompressionLevelValue(Dictionary<string, object>? options)
    {
        if (
            options is not null && 
            options.TryGetValue(COMPRESSION_LEVEL, out var objectValue))
        {
            if (objectValue is int value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{COMPRESSION_LEVEL}' must be of type {nameof(Int32)}.");
        }

        else
        {
            return 0;
        }
    }
}