using BenchmarkDotNet.Attributes;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using ISA_L.PInvoke;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Benchmark;

[MemoryDiagnoser]
public class Inflate
{
    private byte[] _original = default!;
    private byte[] _deflated = default!;
    private byte[] _inflated = default!;

    private MemoryStream _deflatedStream = default!;
    private MemoryStream _inflatedStream = default!;

    private IntPtr _state_ptr;
    private int _state_length;

    [GlobalSetup]
    public unsafe void GlobalSetup()
    {
        var random = new Random(Seed: 0);
        var original = new byte[N];

        random.NextBytes(original);

        static byte[] Deflate(byte[] original)
        {
            using var originalStream = new MemoryStream(original);
            using var compressedStream = new MemoryStream();

            using (var compressionStream = new DeflateStream(compressedStream, CompressionMode.Compress))
            {
                originalStream.CopyTo(compressionStream);
            }

            return compressedStream.ToArray();
        }

        _original = original;
        _deflated = Deflate(original);
        _inflated = new byte[N];

        // create memory streams
        _deflatedStream = new MemoryStream(_deflated);
        _inflatedStream = new MemoryStream(_inflated);

        // create inflate_state
        _state_length = Unsafe.SizeOf<inflate_state>();
        _state_ptr = Marshal.AllocHGlobal(Unsafe.SizeOf<inflate_state>());
        new Span<byte>(_state_ptr.ToPointer(), _state_length).Clear();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _deflatedStream.Dispose();
        _inflatedStream.Dispose();

        Marshal.FreeHGlobal(_state_ptr);
    }

    [Params(1, 100, 10_000, 1_000_000, 10_000_000)]
    public int N;

    [Benchmark(Baseline = true)]
    public Memory<byte> MicrosoftDeflateStream()
    {
        _deflatedStream.Position = 0;
        _inflatedStream.Position = 0;

        using var decompressionStream = new DeflateStream(_deflatedStream, CompressionMode.Decompress, leaveOpen: true);
        decompressionStream.CopyTo(_inflatedStream);

        if (_inflated[0] != _original[0] || _inflated[^1] != _original[^1])
            throw new Exception("Inflate failed.");

        return _inflated;
    }

    [Benchmark]
    public Memory<byte> SharpZipLibInflater()
    {
        _deflatedStream.Position = 0;
        _inflatedStream.Position = 0;

        using var decompressionStream = new InflaterInputStream(_deflatedStream, new Inflater(noHeader: true)) { IsStreamOwner = false };
        decompressionStream.CopyTo(_inflatedStream);

        if (_inflated[0] != _original[0] || _inflated[^1] != _original[^1])
            throw new Exception("Inflate failed.");

        return _inflated;
    }

    [Benchmark]
    public unsafe Memory<byte> Intel_ISA_L_Inflate()
    {
        var stateSpan = new Span<inflate_state>(_state_ptr.ToPointer(), _state_length);
        ref var state = ref stateSpan[0];

        ISAL.isal_inflate_init(_state_ptr);

        var length = 0;

        var sourceBuffer = _deflated.AsSpan();
        var targetBuffer = _inflated.AsSpan();

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

                    var status = ISAL.isal_inflate(_state_ptr);

                    if (status != inflate_return_values.ISAL_DECOMP_OK)
                        throw new Exception($"Error encountered while decompressing: {status}.");

                    length += targetBuffer.Length - (int)state.avail_out;

                    if (state.block_state != isal_block_state.ISAL_BLOCK_FINISH &&
                        state.avail_out == 0)
                    {
                        // double array size
                        var tmp = _inflated;
                        _inflated = new byte[tmp.Length * 2];
                        tmp.CopyTo(_inflated.AsSpan());
                        targetBuffer = _inflated.AsSpan(tmp.Length);
                    }

                    else
                    {
                        break;
                    }
                }
            }
        }

        return _inflated.AsMemory(0, length);
    }
}