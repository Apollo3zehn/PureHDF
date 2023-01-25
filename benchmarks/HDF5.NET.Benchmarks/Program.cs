using BenchmarkDotNet.Running;

namespace Benchmark
{
    public class Program
    {
        static void Main()
        {
            _ = BenchmarkRunner.Run<InflateComparison>();
        }
    }
}
