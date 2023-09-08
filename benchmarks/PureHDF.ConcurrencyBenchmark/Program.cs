// NOTE: The multi-threaded benchmark is not yet working because the chunk searching
// algorithm is not yet thread-safe! E.g. FixedArrayHeader is instantiated during
// a read operation. Solutions: locks, thread-local file handle, RandomAccess.

/*
 * The aim of this benchmark is to proove that concurrent programming is more
 * efficient than single-threaded programming (under certain conditions) in the 
 * context of PureHDF.
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
 * Multithreaded: Uses Parallel.For to load and process the data.
 * 
 * Important notes:
 *
 * 1) The ProcessData() method should simulate a CPU bound task which optimally runs
 * whenever the thread waits for an I/O operation to complete. The things calculated
 * in ProcessData() have been choosen so that it creates a certain CPU load that 
 * matches roughly the time needed the current I/O operation. The optimal CPU load 
 * depends highly on your system setup.
 *
 * To run the benchmarks, make sure that you do not attach the debugger and that you build 
 * the project in Release mode. If you use VSCode, select the "Run Concurrency Benchmark"
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
 * The file-based multi-threaded benchmark took 627,5 ms.
 * The memory-mapped-file based multi-threaded benchmark took 614,8 ms.
 * 
 * The ratio multi-threaded (file) / sync is 0,27.
 *  The ratio multi-threaded (MMF) / sync is 0,27.
 * ======================================================================
 */

using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using PureHDF;
using PureHDF.Selections;

const ulong CHUNK_SIZE = 1 * 1024 * 1024 * 100 / 4; // 4 bytes per value
const ulong CHUNK_COUNT = 10;

const ulong BUFFER_SIZE = CHUNK_SIZE;
const ulong SEGMENT_COUNT = CHUNK_COUNT;

const string FILE_PATH = "/tmp/PureHDF/sync.h5";

try
{
    if (!File.Exists(FILE_PATH))
    {
        Console.WriteLine($"Create test file {FILE_PATH}.");

        var process = new Process();
        process.StartInfo.FileName = "python";
        process.StartInfo.Arguments = $"benchmarks/PureHDF.ConcurrencyBenchmark/create_test_file.py {FILE_PATH}";
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

    // 4 multi-threaded benchmark (file)
    Console.ReadKey(intercept: true);

    (var multiThreadedFileResult, var elapsed_multi_threaded_file) = MultiThreadedBenchmark_File();

    // 5 multi-threaded benchmark (mmf)
    Console.ReadKey(intercept: true);

    (var multiThreadedMMFResult, var elapsed_multi_threaded_mmf) = MultiThreadedBenchmark_MMF();

    //
    if (syncResult != multiThreadedFileResult)
        throw new Exception($"The sync result ({syncResult}) and multi-threaded (file) result ({multiThreadedFileResult}) are not equal.");

    if (syncResult != multiThreadedMMFResult)
        throw new Exception($"The sync result ({syncResult}) and multi-threaded (MMF) result ({multiThreadedMMFResult}) are not equal.");

    Console.WriteLine();
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
        FileShare.Read
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

        dataset.Read(buffer, fileSelection);

        result += ProcessData(buffer);
    }

    var elapsed_sync = stopwatch.Elapsed;
    Console.WriteLine($"The sync benchmark took {elapsed_sync.TotalMilliseconds:F1} ms.");

    return (result, elapsed_sync);
}

(double, TimeSpan) MultiThreadedBenchmark_File()
{
    var result = 0.0;
    var syncObject = new object();

    using var file = H5File.Open(
        FILE_PATH,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read
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

        dataset.Read(buffer, fileSelection);

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

        dataset.Read(buffer, fileSelection);

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