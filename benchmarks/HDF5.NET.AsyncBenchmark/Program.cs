/*
 * This benchmark was created on a system with the following specs:
 * - 11th Gen Intel(R) Core(TM) i7-1165G7 @ 2.80GHz
 * - M2 SSD
 * - Kubuntu 22.10
 *
 * The aim of this benchmark is to proove that async programming is more efficient 
 * than sync programming (under certain conditions) in the context of HDF5.NET.
 *
 * To make that proof, this program first runs a python script to create test data
 * (the package h5py is required). Two files are created so that both tests - sync
 * and async - read from unique files to avoid that the file is already cached for
 * the second test.
 *
 * After that the user is asked to clear the linux file cache using the provided
 * command. When that is done, the tests run:
 *
 * Sync test: This test slices the whole file data into several blocks for reading. 
 * When a block is done reading, it gets processed and after that the next block is
 * read, etc.
 * 
 * Async test: This test also slices the whole file data into blocks. However, it 
 * utilizes the System.IO.Pipelines package to asynchronously process data. The data
 * of the just read block are pushed into the pipeline where another task processes
 * it. The big advantage here is that while the thread asynchronously waits for the
 * next data block to become available, it can start processing the prevously read
 * data. 
 *
 * In the end the async test should be faster when only a single thread is involved.
 * Of course, with two or more threads, there would be no big difference between the
 * sync test and the async test. However, the purpose of async programming is to 
 * explicitly improve efficiency of a thread that waits for an I/O operation to complete.
 *
 * Important notes:
 *
 * 1) To ensure that only a single thread is created, the class 
 * System.Threading.Tasks.Schedulers.LimitedConcurrencyLevelTaskScheduler of the 
 * ParallelExtensionsExtras.NetFxStandard package is utilized.
 *
 * 2) The ProcessData() method should simiulate a CPU bound task which optimally runs
 * whenever the thread waits for an I/O operation to complete. The things calculated
 * in ProcessData() have been choosen so that it creates a certain CPU load that 
 * matches roughly the time needed the current I/O operation. The optimal CPU load 
 * depends highly on your system setup.
 *
 * To run the test, make sure that you do not attach the debugger and that you build 
 * the project in Release mode. If you use VSCode, select the "Run Async Benchmark"
 * configuration and start it using Ctrl + F5 (to no attach the debugger).
 *
 * Results (2023-01-09)
 * ====================
 * Create test file /tmp/HDF5.NET/sync.h5.
 * Create test file /tmp/HDF5.NET/async.h5.
 * Please run the following command to clear the file cache and monitor the cache usage:
 * free -wh && sync && echo 1 | sudo sysctl vm.drop_caches=1 && free -wh
 * 
 * Press any key to continue ...
 * Run sync test.
 * The sync test took 227,1 ms. The result is 2,830E+010.
 * 
 * Run async test.
 * The async test took 170,5 ms. The result is 2,830E+010.
 * The pure processing time was 135,6 ms.
 * 
 * The ratio async / sync is 0,75.
 * 
 * Clean up test files.
 * ====================
 */


using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Schedulers;
using HDF5.NET;

const ulong CHUNK_SIZE = 1 * 1024 * 1024 * 10 / 4; // = 256 kb 4 bytes per value
const ulong CHUNK_COUNT = 10;

const ulong BUFFER_SIZE = CHUNK_SIZE;
const ulong BUFFER_BYTE_SIZE = BUFFER_SIZE * sizeof(float);
const ulong SEGMENT_COUNT = CHUNK_COUNT;

var syncFilePath = "/tmp/HDF5.NET/sync.h5";
var asyncFilePath = "/tmp/HDF5.NET/async.h5";

try
{
    // 1. create files
    foreach (var filePath in new string[] { syncFilePath, asyncFilePath })
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Create test file {filePath}.");

            var process = new Process();
            process.StartInfo.FileName = "python";
            process.StartInfo.Arguments = $"benchmarks/HDF5.NET.AsyncBenchmark/create_test_file.py {filePath}";
            process.Start();
            process.WaitForExit();

            var exitCode = process.ExitCode;

            if (exitCode != 0)
                throw new Exception("Unable to create test files.");
        }

        else
        {
            Console.WriteLine($"No need to create test file {filePath} as it is already there.");
        }
    }

    // 2. ask user to clear cache
    // https://medium.com/marionete/linux-disk-cache-was-always-there-741bef097e7f
    // https://unix.stackexchange.com/a/82164
    Console.WriteLine("Please run the following command to clear the file cache and monitor the cache usage:");
    Console.WriteLine("free -wh && sync && echo 1 | sudo sysctl vm.drop_caches=1 && free -wh");
    Console.WriteLine();
    Console.WriteLine("Press any key to continue ...");

    Console.ReadKey(intercept: true);

    // 3. sync test
    var syncResult = 0.0;

    using var file_sync = H5File.Open(
        syncFilePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        useAsync: false
    );

    var dataset_sync = file_sync.Dataset("chunked");

    Console.WriteLine($"Run sync test.");

    var syncBuffer = Enumerable.Range(0, (int)BUFFER_SIZE).Select(value => (float)value).ToArray();
    var stopwatch_sync = Stopwatch.StartNew();

    for (uint i = 0; i < SEGMENT_COUNT; i++)
    {
        var fileSelection = new HyperslabSelection(
            start: i * BUFFER_SIZE,
            block: BUFFER_SIZE
        );

        dataset_sync.Read<float>(syncBuffer, fileSelection);

        syncResult += ProcessData(syncBuffer);
    }

    var elapsed_sync = stopwatch_sync.Elapsed;
    Console.WriteLine($"The sync test took {elapsed_sync.TotalMilliseconds:F1} ms. The result is {syncResult:E3}.");

    // 4. async test
    var asyncResult = 0.0;

    using var file_async = H5File.Open(
        asyncFilePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        useAsync: true
    );

    var dataset_async = file_async.Dataset("chunked");
    var scheduler = new LimitedConcurrencyLevelTaskScheduler(maxDegreeOfParallelism: 1);
    var threadId = Thread.CurrentThread.ManagedThreadId;

    Console.WriteLine();
    Console.WriteLine($"Run async test.");

    var stopwatch_async = Stopwatch.StartNew();

    var options = new PipeOptions(
        // avoid blocking!
        // default is 65536 (https://github.com/dotnet/runtime/blob/55e1ac7c07df62c4108d4acedf78f77574470ce5/src/libraries/System.IO.Pipelines/src/System/IO/Pipelines/PipeOptions.cs#L48)
        pauseWriterThreshold: 0,
        useSynchronizationContext: false);

    var pipe = new Pipe(options);
    var reader = pipe.Reader;
    var writer = pipe.Writer;

    var reading = Task.Factory.StartNew(async () =>
    {
        for (uint i = 0; i < SEGMENT_COUNT; i++)
        {
            var asyncBuffer = new CastMemoryManager<byte, float>(writer.GetMemory((int)BUFFER_BYTE_SIZE)).Memory;

            var fileSelection = new HyperslabSelection(
                start: i * BUFFER_SIZE,
                block: BUFFER_SIZE
            );

            await dataset_async.ReadAsync<float>(asyncBuffer, fileSelection);

            writer.Advance((int)BUFFER_BYTE_SIZE);
            await writer.FlushAsync();
        }

        await writer.CompleteAsync();
    }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler).Unwrap();

    var processingTime = TimeSpan.Zero;

    var processing = Task.Factory.StartNew(async () =>
    {
        while (true)
        {
            var result = await reader.ReadAsync();
            var asyncBuffer = result.Buffer;
            var processingTimeSw = Stopwatch.StartNew();

            asyncResult += ProcessData(MemoryMarshal.Cast<byte, float>(asyncBuffer.First.Span));    
            
            processingTime += processingTimeSw.Elapsed;

            if (result.Buffer.Length == 0)
                reader.AdvanceTo(
                    consumed: asyncBuffer.Start,
                    examined: asyncBuffer.End);
            else
                reader.AdvanceTo(
                    consumed: asyncBuffer.GetPosition((long)BUFFER_BYTE_SIZE), 
                    examined: asyncBuffer.End);

            if (result.Buffer.Length == 0)
                break;
        }

        await reader.CompleteAsync();
    }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler).Unwrap();

    await Task.WhenAll(reading, processing);

    var elapsed_async = stopwatch_async.Elapsed;
    Console.WriteLine($"The async test took {elapsed_async.TotalMilliseconds:F1} ms. The result is {asyncResult:E3}.");
    Console.WriteLine($"The pure processing time was {processingTime.TotalMilliseconds:F1} ms.");

    //
    Console.WriteLine();
    Console.WriteLine($"The ratio async / sync is {(elapsed_async.TotalMilliseconds / elapsed_sync.TotalMilliseconds):F2}.");
}
finally
{
    Console.WriteLine();
    Console.WriteLine($"Clean up test files.");

    if (File.Exists(syncFilePath))
    {
        try { File.Delete(syncFilePath); }
        catch { }
    }

    if (File.Exists(asyncFilePath))
    {
        try { File.Delete(asyncFilePath); }
        catch { }
    }
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