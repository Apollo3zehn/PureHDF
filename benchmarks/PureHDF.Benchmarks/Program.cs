using BenchmarkDotNet.Running;

namespace Benchmark
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Any())
            {
                _ = args[0] switch
                {
                    "inflate" => BenchmarkRunner.Run<Inflate>(),
                    "walk" => BenchmarkRunner.Run<Walk>(),
                    "shuffle" => BenchmarkRunner.Run<Shuffle>(),
                    _ => throw new Exception($"Unknown benchmark {args[0]}")
                };
            }
        }
    }
}
