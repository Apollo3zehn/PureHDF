using BenchmarkDotNet.Running;

namespace Benchmark
{
    public class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<InflateComparison>();
        }
    }
}
