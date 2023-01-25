using BenchmarkDotNet.Running;

namespace Benchmark
{
    public class Program
    {
        static void Main(string[] args)
        {
            _ = BenchmarkRunner.Run<InflateComparison>();
        }
    }
}
