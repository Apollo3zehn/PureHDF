using System.Threading.Tasks.Schedulers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HDF5.NET;

namespace Benchmark
{
// TODO: This benchmark does not prove that Async is actually faster. Maybe create files with different chunk size to evaluate the influence of a the chunk reads. If reading all chunks is very fast, e.g. ~1 ms, the results (sync 9.8 ms, async 8.7 ms) could be explained. Solution: spend more time reading data and less calculating the sum.
// TODO: There are many GC Gen 0 .. Gen 2 collections in this benchmark. Why?

    /* This test aims to prove that the async methods really run asynchronous. The
     * idea is that while a chunk from the HDF5 file is loaded, the previous chunk
     * can already be processed.

     * One challenge is to avoid that the task scheduler just spawns additional threads.
     * This is addressed by making use of the LimitedConcurrencyLevelTaskScheduler.
     * 
     * Another challenge is to use a non-cached file stream. This is addressed by mounting
     * a drive with the sync option:
     * sudo mount -o uid=1000,gid=1000,sync /dev/sdb1 /mnt/sync
     *
     * Alternative: File.WriteAllText("/proc/sys/vm/drop_caches", "3"); but that needs write access.
     *
     * The last challenge is to make the time needed to load the data nearly equal
     * to the time needed to process the data. This setup would make maximum use
     * of the freed CPU cycles using async methods.
     *
     * > Still unsure if the file system access is uncached (does the sync flag really work?).
     * Easiest way would be to run this benchmark on Windows (http://saplin.blogspot.com/2018/07/non-cachedunbuffered-file-operations.html)
     */

    [SimpleJob(RuntimeMoniker.Net70)]
    [MemoryDiagnoser]
    public class AsyncPerformance
    {
        private H5File _file_sync = default!;
        private H5File _file_async = default!;
        private H5Dataset _dataset_sync = default!;
        private H5Dataset _dataset_async = default!;
        private LimitedConcurrencyLevelTaskScheduler _scheduler = default!;

        private const ulong CHUNK_SIZE = 1 * 1024 * 256; // 4 bytes per value
        private const ulong CHUNK_COUNT = 10;
        private float[] _buffer1 = Enumerable.Range(0, (int)CHUNK_SIZE).Select(value => (float)value).ToArray();
        private float[] _buffer2 = new float[CHUNK_SIZE];

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
            var filePath = "/mnt/sync/large_and_chunked.h5";

            // sync
            _file_sync = H5File.Open(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                useAsync: false
            );

            _dataset_sync = _file_sync.Dataset("chunked");

            // async
            _file_async = H5File.Open(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                useAsync: true
            );

            _dataset_async = _file_async.Dataset("chunked");

            // create custom task factory which allows only a single thread
            _scheduler = new LimitedConcurrencyLevelTaskScheduler(maxDegreeOfParallelism: 1);
        }

        [GlobalCleanup]
        public unsafe void GlobalCleanup()
        {
            _file_sync.Dispose();
            _file_async.Dispose();
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

                _dataset_sync.Read<float>(_buffer2, fileSelection);

                result += SumData(_buffer1);
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
                    await _dataset_async.ReadAsync<float>(_buffer2, fileSelection);
                    var localResult = SumData(_buffer1);

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
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (float)Math.Pow(data[i], 2);
            }

            return data.Sum();
        }
    }
}