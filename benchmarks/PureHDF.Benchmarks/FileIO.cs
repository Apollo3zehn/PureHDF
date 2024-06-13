using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    public ulong FileStream_Seek()
    {
        var result = 0UL;
        var size = Unsafe.SizeOf<ulong>();
        Span<byte> buffer = stackalloc byte[size];

        foreach (var position in _positions)
        {
            _fileStream.Seek(position, SeekOrigin.Begin);
            _fileStream.Read(buffer);

            result += MemoryMarshal.Cast<byte, ulong>(buffer)[0];
        }

        return result;
    }

    [Benchmark]
    public ulong FileStream_Position()
    {
        var result = 0UL;
        var size = Unsafe.SizeOf<ulong>();
        Span<byte> buffer = stackalloc byte[size];

        foreach (var position in _positions)
        {
            _fileStream.Position = position;
            _fileStream.Read(buffer);

            result += MemoryMarshal.Cast<byte, ulong>(buffer)[0];
        }

        return result;
    }

    [Benchmark]
    public ulong SafeFileHandle()
    {
        var result = 0UL;
        var size = Unsafe.SizeOf<ulong>();
        Span<byte> buffer = stackalloc byte[size];

        foreach (var position in _positions)
        {
            RandomAccess.Read(_safeFileHandle, buffer, position);

            result += MemoryMarshal.Cast<byte, ulong>(buffer)[0];
        }

        return result;
    }
}