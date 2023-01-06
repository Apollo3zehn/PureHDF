using System.Threading.Tasks.Schedulers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HDF5.NET;

namespace Benchmark
{
// TODO: This benchmark does not prove that Async is actually faster. Maybe create files with different chunk size to evaluate the influence of a the chunk reads. If reading all chunks is very fast, e.g. ~1 ms, the results (sync 9.8 ms, async 8.7 ms) could be explained. Solution: spend more time reading data and less calculating the sum.
// TODO: There are many GC Gen 0 .. Gen 2 collections in this benchmark. Why?

    [SimpleJob(RuntimeMoniker.Net70)]
    [MemoryDiagnoser]
    public class AsyncPerformance
    {
        private H5File _file = default!;
        private H5Dataset _dataset = default!;
        private LimitedConcurrencyLevelTaskScheduler _scheduler = default!;

        private const ulong CHUNK_SIZE = 1 * 1024 * 256; // 4 bytes per value
        private const ulong CHUNK_COUNT = 10;
        private float[] _buffer = new float[CHUNK_SIZE];

        [GlobalSetup]
        public unsafe void GlobalSetup()
        {
            // LimitedConcurrencyLevelTaskScheduler
            // https://devblogs.microsoft.com/pfxteam/parallelextensionsextras-tour-7-additional-taskschedulers/

            /* create test file (python):

                import h5py

                h5File = h5py.File('large_and_chunked.h5','w')
                chunk_size = 1 * 1024 * 256 # 4 bytes per value; equal to the SimpleChunkCache default size
                chunk_count = 10

                dataset = h5File.create_dataset(
                    name="chunked",
                    shape=(chunk_count * chunk_size,),
                    chunks=(chunk_size,))

                dataset[...] = range(0, chunk_count * chunk_size)

            */

            // open H5 file
            var filePath = "/home/vincent/Downloads/test/large_and_chunked.h5";

            _file = H5File.Open(
                filePath,
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                useAsync: true);

            _dataset = _file.Dataset("chunked");

            // create custom task factory which allows only a single thread
            _scheduler = new LimitedConcurrencyLevelTaskScheduler(maxDegreeOfParallelism: 1);
        }

        [GlobalCleanup]
        public unsafe void GlobalCleanup()
        {
            _file.Dispose();
        }

        [Benchmark(Baseline = true)]
        public float Sync()
        {
            var result = 0.0f;

            for (uint i = 0; i < CHUNK_COUNT; i++)
            {
                var fileSelection = new HyperslabSelection(
                    start: i * CHUNK_SIZE,
                    block: CHUNK_SIZE
                );

                _dataset.Read<float>(_buffer, fileSelection);
                result += SumData(_buffer);
            }

            return result;
        }

        [Benchmark()]
        public async Task<float> Async()
        {
            var result = 0.0f;
            var tasks = new List<Task<float>>();

            for (uint i = 0; i < CHUNK_COUNT; i++)
            {
                var fileSelection = new HyperslabSelection(
                    start: i * CHUNK_SIZE,
                    block: CHUNK_SIZE
                );

                var task = Task.Factory.StartNew(async () =>
                {
                    await _dataset.ReadAsync<float>(_buffer, fileSelection);
                    var localResult = SumData(_buffer);

                    return localResult;
                }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, _scheduler).Unwrap();

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            result = results.Sum();

            return result;
        }

        private float SumData(float[] data)
        {
            return data.Sum();
        }
    }
}