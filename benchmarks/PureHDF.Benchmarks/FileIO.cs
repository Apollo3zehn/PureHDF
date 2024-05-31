using BenchmarkDotNet.Attributes;
using Microsoft.Win32.SafeHandles;

namespace Benchmark;

// https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/

public class FileIO
{
    private const int SIZE = 1024 * 1024;

    private readonly static Random _random = new();

    private static readonly int[] _positions = Enumerable
        .Range(0, 100)
        .Select(i => _random.Next(0, SIZE))
        .ToArray();

    private readonly byte[] _buffer = new byte[1];

    private FileStream _fileStream = default!;

    private SafeFileHandle _safeFileHandle = default!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var fileName = "test.bin";

        using (var targetStream = File.OpenWrite(fileName))
        {
            targetStream.SetLength(SIZE);
        };

        _fileStream = File.OpenRead(fileName);
        _safeFileHandle = _fileStream.SafeFileHandle;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _fileStream.Close();
    }

    [Benchmark(Baseline = true)]
    public void FileStream_Seek()
    {
        foreach (var position in _positions)
        {
            _fileStream.Seek(position, SeekOrigin.Begin);
            _fileStream.Read(_buffer);
        }
    }

    [Benchmark]
    public void FileStream_Position()
    {
        foreach (var position in _positions)
        {
            _fileStream.Position = position;
            _fileStream.Read(_buffer);
        }
    }

    [Benchmark]
    public void SafeFileHandle()
    {
        foreach (var position in _positions)
        {
            RandomAccess.Read(_safeFileHandle, _buffer, position);
        }
    }
}