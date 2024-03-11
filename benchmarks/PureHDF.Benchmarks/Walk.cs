using BenchmarkDotNet.Attributes;
using PureHDF.Selections;

namespace Benchmark;

[MemoryDiagnoser]
public class Walk
{
    public ulong[] Dims = [1000, 200, 200];

    public ulong[] ChunkDims = [1000, 200, 200];

    public HyperslabSelection Selection = new(
        rank: 3,
        starts: [1, 1, 1],
        strides: [31, 31, 31],
        counts: [30, 6, 6],
        blocks: [25, 26, 27]
    );

    [Benchmark(Baseline = true)]
    public int Execute()
    {
        var steps = SelectionHelper
            .Walk(rank: 3, Dims, ChunkDims, Selection)
            .ToArray();

        return steps.Length;
    }
}