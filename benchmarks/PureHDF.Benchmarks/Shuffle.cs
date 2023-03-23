using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using PureHDF;
using PureHDF.Filters;

namespace Benchmark
{
    [MemoryDiagnoser]
    public class Shuffle
    {
        private readonly int _bytesOfType = sizeof(long);
        private long[] _shuffledData = default!;
        private long[] _unshuffledData = default!;

        [GlobalSetup]
        public unsafe void GlobalSetup()
        {
            if (!Sse2.IsSupported)
                throw new Exception("SSE2 is not supported on this platform.");

            if (!Avx2.IsSupported)
                throw new Exception("AVX2 is not supported on this platform.");

            var random = new Random();

            var source = Enumerable
                .Range(0, N)
                .Select(i => random.NextInt64())
                .ToArray();

            var destination = Enumerable
                .Range(0, N)
                .Select(i => 0L)
                .ToArray();

            ShuffleAvx2.Shuffle(
                _bytesOfType, 
                MemoryMarshal.AsBytes<long>(source),
                MemoryMarshal.AsBytes<long>(destination));

            _shuffledData = destination;

            _unshuffledData = Enumerable
                .Range(0, N)
                .Select(i => 0L)
                .ToArray();
        }

        [Params(1, 100, 10_000, 1_000_000, 10_000_000)]
        public int N;

        [Benchmark(Baseline = true)]
        public Memory<long> Generic()
        {
            ShuffleGeneric.Unshuffle(
                _bytesOfType,
                MemoryMarshal.AsBytes<long>(_shuffledData), 
                MemoryMarshal.AsBytes<long>(_unshuffledData));

            return _unshuffledData;
        }

        [Benchmark]
        public Memory<long> SSE2()
        {
            ShuffleSse2.Unshuffle(
                _bytesOfType,
                MemoryMarshal.AsBytes<long>(_shuffledData), 
                MemoryMarshal.AsBytes<long>(_unshuffledData));

            return _unshuffledData;
        }

        [Benchmark]
        public Memory<long> AVX2()
        {
            ShuffleAvx2.Unshuffle(
                _bytesOfType,
                MemoryMarshal.AsBytes<long>(_shuffledData), 
                MemoryMarshal.AsBytes<long>(_unshuffledData));

            return _unshuffledData;
        }
    }
}