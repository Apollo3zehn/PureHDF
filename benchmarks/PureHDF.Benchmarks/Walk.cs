using BenchmarkDotNet.Attributes;
using PureHDF;

namespace Benchmark
{
    [MemoryDiagnoser]
    public class Walk
    {
        public ulong[] Dims = new ulong[] { 1000, 200, 200 };

        public ulong[] ChunkDims = new ulong[] { 1000, 200, 200 };

        public HyperslabSelection Selection = new(
            rank: 3,
            starts: new ulong[] { 1, 1, 1 },
            strides: new ulong[] { 31, 31, 31 },
            counts: new ulong[] { 30, 6, 6 },
            blocks: new ulong[] { 25, 26, 27 }
        );

        [Benchmark(Baseline = true)]
        public int Execute()
        {
            var steps = SelectionUtils
                .Walk(rank: 3, Dims, ChunkDims, Selection)
                .ToArray();

            return steps.Length;
        }
    }
}