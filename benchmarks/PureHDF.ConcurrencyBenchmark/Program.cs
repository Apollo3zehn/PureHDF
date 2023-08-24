// NOTE: The multi-threaded benchmark is not yet working because the chunk searching
// algorithm is not yet thread-safe! E.g. FixedArrayHeader is instantiated during
// a read operation. Solutions: locks, thread-local file handle, RandomAccess.

/*
 * The aim of this benchmark is to proove that async programming is more efficient 
 * than sync programming (under certain conditions) in the context of PureHDF. The
 * performance is also compared to a multi-threaded approach.
 *
 * To make that proof, this program first runs a python script to create test data
 * (the package h5py is required). A single file is created which means that the
 * linux file cache needs to be cleared before each benchmark runs.
 *
 * The following benchmarks are executed:
 *
 * Sync: This benchmark slices the whole file data into several blocks for reading. 
 * When a block is done reading, it gets processed and after that the next block is
 * read, etc.
 * 
 * Async: This benchmark also slices the whole file data into blocks. However, it 
 * utilizes the System.IO.Pipelines package to asynchronously process data. The data
 * of the just read block are pushed into the pipeline where another task processes
 * it. The big advantage here is that while the thread asynchronously waits for the
 * next data block to become available, it can start processing the prevously read
 * data. 
 *
 * Task-based: Works the same as the async benchmarks but with the default scheduler
 * which uses the thread-pool to run the tasks.
 * 
 * Multithreaded: Uses Parallel.For to load and process the data.
 * 
 * In the end the async benchmarks should be faster when only a single thread is involved.
 * Of course, with two or more threads, there would be no big difference between the
 * sync test and the async test. However, the purpose of async programming is to 
 * explicitly improve efficiency of a thread that waits for an I/O operation to complete.
 *
 * Important notes:
 *
 * 1) To ensure that only a single thread is created for the async benchmark, the class 
 * System.Threading.Tasks.Schedulers.LimitedConcurrencyLevelTaskScheduler of the 
 * ParallelExtensionsExtras.NetFxStandard package is utilized.
 *
 * 2) The ProcessData() method should simiulate a CPU bound task which optimally runs
 * whenever the thread waits for an I/O operation to complete. The things calculated
 * in ProcessData() have been choosen so that it creates a certain CPU load that 
 * matches roughly the time needed the current I/O operation. The optimal CPU load 
 * depends highly on your system setup.
 *
 * To run the benchmarks, make sure that you do not attach the debugger and that you build 
 * the project in Release mode. If you use VSCode, select the "Run Async Benchmark"
 * configuration and start it using Ctrl + F5 (to no attach the debugger).
 *
 * ======================================================================
 * Results (2023-01-25)
 * ======================================================================
 * This benchmark was created on a system with the following specs:
 * - 11th Gen Intel(R) Core(TM) i7-1165G7 @ 2.80GHz
 * - M2 SSD
 * - Kubuntu 22.10
 * ======================================================================
 * The sync benchmark took 2289,4 ms.
 * The async benchmark took 1482,5 ms.
 * The task-based benchmark took 1406,7 ms.
 * The file-based multi-threaded benchmark took 627,5 ms.
 * The memory-mapped-file based multi-threaded benchmark took 614,8 ms.
 * 
 *                 The ratio async / sync is 0,65.
 *            The ratio task-based / sync is 0,61.
 * The ratio multi-threaded (file) / sync is 0,27.
 *  The ratio multi-threaded (MMF) / sync is 0,27.
 * ======================================================================
 */

using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Schedulers;
using PureHDF;
using PureHDF.Selections;

const ulong CHUNK_SIZE = 1 * 1024 * 1024 * 100 / 4; // 4 bytes per value
const ulong CHUNK_COUNT = 10;

const ulong BUFFER_SIZE = CHUNK_SIZE;
const ulong BUFFER_BYTE_SIZE = BUFFER_SIZE * sizeof(float);
const ulong SEGMENT_COUNT = CHUNK_COUNT;

const string FILE_PATH = "/tmp/PureHDF/sync.h5";

try
{
    if (!File.Exists(FILE_PATH))
    {
        Console.WriteLine($"Create test file {FILE_PATH}.");

        var process = new Process();
        process.StartInfo.FileName = "python";
        process.StartInfo.Arguments = $"benchmarks/PureHDF.AsyncBenchmark/create_test_file.py {FILE_PATH}";
        process.Start();
        process.WaitForExit();

        var exitCode = process.ExitCode;

        if (exitCode != 0)
            throw new Exception("Unable to create test files.");
    }

    else
    {
        Console.WriteLine($"No need to create test file {FILE_PATH} as it is already there.");
    }

    // 2 ask user to clear cache
    // https://medium.com/marionete/linux-disk-cache-was-always-there-741bef097e7f
    // https://unix.stackexchange.com/a/82164
    Console.WriteLine("Please run the following command before every benchmark step to clear the file cache:");
    Console.WriteLine("free -wh && sync && echo 1 | sudo sysctl vm.drop_caches=1 && free -wh");
    Console.WriteLine();
    Console.WriteLine("Press any key to continue.");

    // 3 sync benchmark
    Console.ReadKey(intercept: true);

    (var syncResult, var elapsed_sync) = SyncBenchmark();

    // 4 async benchmark
    Console.ReadKey(intercept: true);

    var scheduler = new LimitedConcurrencyLevelTaskScheduler(maxDegreeOfParallelism: 1);

    Task startTask(Func<Task> task)
        => Task.Factory
            .StartNew(task, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler)
            .Unwrap();

    (var asyncResult, var elapsed_async) = await TaskBasedBenchmark("async", startTask);

    // 5 task based benchmark
    Console.ReadKey(intercept: true);

    (var taskBasedResult, var elapsed_task_based) = await TaskBasedBenchmark("task-based", Task.Run);

    // 6 multi-threaded benchmark (file)
    Console.ReadKey(intercept: true);

    (var multiThreadedFileResult, var elapsed_multi_threaded_file) = MultiThreadedBenchmark_File();

    // 7 multi-threaded benchmark (mmf)
    Console.ReadKey(intercept: true);

    (var multiThreadedMMFResult, var elapsed_multi_threaded_mmf) = MultiThreadedBenchmark_MMF();

    //
    if (syncResult != asyncResult)
        throw new Exception($"The sync result ({syncResult}) and async result ({asyncResult}) are not equal.");

    if (syncResult != taskBasedResult)
        throw new Exception($"The sync result ({syncResult}) and task-based result ({taskBasedResult}) are not equal.");

    if (syncResult != multiThreadedFileResult)
        throw new Exception($"The sync result ({syncResult}) and multi-threaded (file) result ({multiThreadedFileResult}) are not equal.");

    if (syncResult != multiThreadedMMFResult)
        throw new Exception($"The sync result ({syncResult}) and multi-threaded (MMF) result ({multiThreadedMMFResult}) are not equal.");

    Console.WriteLine();
    Console.WriteLine($"                The ratio async / sync is {elapsed_async.TotalMilliseconds / elapsed_sync.TotalMilliseconds:F2}.");
    Console.WriteLine($"           The ratio task-based / sync is {elapsed_task_based.TotalMilliseconds / elapsed_sync.TotalMilliseconds:F2}.");
    Console.WriteLine($"The ratio multi-threaded (file) / sync is {elapsed_multi_threaded_file.TotalMilliseconds / elapsed_sync.TotalMilliseconds:F2}.");
    Console.WriteLine($" The ratio multi-threaded (MMF) / sync is {elapsed_multi_threaded_mmf.TotalMilliseconds / elapsed_sync.TotalMilliseconds:F2}.");
}
finally
{
    Console.WriteLine();
    Console.WriteLine($"Clean up test file.");

    if (File.Exists(FILE_PATH))
    {
        try { File.Delete(FILE_PATH); }
        catch { }
    }
}

(double, TimeSpan) SyncBenchmark()
{
    var result = 0.0;

    using var file = H5File.Open(
        FILE_PATH,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        useAsync: false
    );

    var dataset = file.Dataset("chunked");
    var buffer = new float[BUFFER_SIZE];
    var stopwatch = Stopwatch.StartNew();

    for (uint i = 0; i < SEGMENT_COUNT; i++)
    {
        var fileSelection = new HyperslabSelection(
            start: i * BUFFER_SIZE,
            block: BUFFER_SIZE
        );

        dataset.Read<float>(buffer, fileSelection);

        result += ProcessData(buffer);
    }

    var elapsed_sync = stopwatch.Elapsed;
    Console.WriteLine($"The sync benchmark took {elapsed_sync.TotalMilliseconds:F1} ms.");

    return (result, elapsed_sync);
}

async Task<(double, TimeSpan)> TaskBasedBenchmark(string name, Func<Func<Task>, Task> startTask)
{
    var result = 0.0;

    using var file = H5File.Open(
        FILE_PATH,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        useAsync: true
    );

    var dataset = file.Dataset("chunked");
    var stopwatch = Stopwatch.StartNew();

    var options = new PipeOptions(
        // avoid blocking!
        // default is 65536 (https://github.com/dotnet/runtime/blob/55e1ac7c07df62c4108d4acedf78f77574470ce5/src/libraries/System.IO.Pipelines/src/System/IO/Pipelines/PipeOptions.cs#L48)
        pauseWriterThreshold: 0,
        useSynchronizationContext: false);

    var pipe = new Pipe(options);
    var reader = pipe.Reader;
    var writer = pipe.Writer;

    var readingAction = async () =>
    {
        for (uint i = 0; i < SEGMENT_COUNT; i++)
        {
            var asyncBuffer = new CastMemoryManager<byte, float>(writer.GetMemory((int)BUFFER_BYTE_SIZE)).Memory;

            var fileSelection = new HyperslabSelection(
                start: i * BUFFER_SIZE,
                block: BUFFER_SIZE
            );

            await dataset.ReadAsync(asyncBuffer, fileSelection);

            writer.Advance((int)BUFFER_BYTE_SIZE);
            await writer.FlushAsync();
        }

        await writer.CompleteAsync();
    };

    var reading = startTask(readingAction);
    var processingTime = TimeSpan.Zero;

    var procssingAction = async () =>
    {
        while (true)
        {
            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;

            if (buffer.Length == 0)
                break;

            var processingTimeSw = Stopwatch.StartNew();

            result += ProcessData(MemoryMarshal.Cast<byte, float>(buffer.First.Span));
            processingTime += processingTimeSw.Elapsed;

            reader.AdvanceTo(
                consumed: buffer.GetPosition((long)BUFFER_BYTE_SIZE),
                examined: buffer.End);
        }

        await reader.CompleteAsync();
    };

    var processing = startTask(procssingAction);

    await Task.WhenAll(reading, processing);

    var elapsed = stopwatch.Elapsed;
    Console.WriteLine($"The {name} benchmark took {elapsed.TotalMilliseconds:F1} ms.");
    // Console.WriteLine($"The pure processing time was {processingTime.TotalMilliseconds:F1} ms.");

    return (result, elapsed);
}

(double, TimeSpan) MultiThreadedBenchmark_File()
{
    var result = 0.0;
    var syncObject = new object();

    using var file = H5File.Open(
        FILE_PATH,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        useAsync: false
    );

    var dataset = file.Dataset("chunked");

    /* The buffer has the size of a single chunk. The reason is
     * that creating a very large buffer slows down the test
     * dramatically (factor ~10). The test file is very large (1GB),
     * and maybe this confuses the garbage collector. A definite
     * cause could not be found. A real world application should
     * rely on the MemoryPool to get an array and maybe limit the
     * number of concurrent threads.
     */
    var buffer = new float[BUFFER_SIZE];
    var stopwatch = Stopwatch.StartNew();

    Parallel.For(0, (int)SEGMENT_COUNT, i =>
    {
        var fileSelection = new HyperslabSelection(
            start: (ulong)i * BUFFER_SIZE,
            block: BUFFER_SIZE
        );

        dataset.Read<float>(buffer, fileSelection);

        var currentResult = ProcessData(buffer);

        lock (syncObject)
        {
            result += currentResult;
        }
    });

    var elapsed_sync = stopwatch.Elapsed;
    Console.WriteLine($"The file-based multi-threaded benchmark took {elapsed_sync.TotalMilliseconds:F1} ms.");

    return (result, elapsed_sync);
}

(double, TimeSpan) MultiThreadedBenchmark_MMF()
{
    var result = 0.0;
    var syncObject = new object();

    using var mmf = MemoryMappedFile.CreateFromFile(FILE_PATH);
    using var accessor = mmf.CreateViewAccessor();
    using var file = H5File.Open(accessor);

    var dataset = file.Dataset("chunked");

    /* The buffer has the size of a single chunk. The reason is
     * that creating a very large buffer slows down the test
     * dramatically (factor ~10). The test file is very large (1GB),
     * and maybe this confuses the garbage collector. A definite
     * cause could not be found. A real world application should
     * rely on the MemoryPool to get an array and maybe limit the
     * number of concurrent threads.
     */
    var buffer = new float[BUFFER_SIZE];
    var stopwatch = Stopwatch.StartNew();

    Parallel.For(0, (int)SEGMENT_COUNT, i =>
    {
        var fileSelection = new HyperslabSelection(
            start: (ulong)i * BUFFER_SIZE,
            block: BUFFER_SIZE
        );

        dataset.Read<float>(buffer, fileSelection);

        var currentResult = ProcessData(buffer);

        lock (syncObject)
        {
            result += currentResult;
        }
    });

    var elapsed_sync = stopwatch.Elapsed;
    Console.WriteLine($"The memory-mapped-file based multi-threaded benchmark took {elapsed_sync.TotalMilliseconds:F1} ms.");

    return (result, elapsed_sync);
}

double ProcessData(ReadOnlySpan<float> data)
{
    var sum = 0.0;

    for (int i = 0; i < data.Length; i++)
    {
        sum += Math.Sqrt(data[i]);
    }

    return sum;
}

internal class CastMemoryManager<TFrom, TTo> : MemoryManager<TTo>
            where TFrom : struct
            where TTo : struct
{
    private readonly Memory<TFrom> _from;

    public CastMemoryManager(Memory<TFrom> from) => _from = from;

    public override Span<TTo> GetSpan() => MemoryMarshal.Cast<TFrom, TTo>(_from.Span);

    protected override void Dispose(bool disposing)
    {
        //
    }

    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

    public override void Unpin() => throw new NotSupportedException();
}