using BenchmarkDotNet.Attributes;
using PureHDF;

namespace Benchmark;

public class chunked_no_filter
{
    private IH5Dataset _dataset = default!;
    private readonly long[] _buffer = new long[1024 * 100];

    [GlobalSetup]
    public void GlobalSetup()
    {
        var fileName = "chunked_no_filter.h5";

        using (var client = new HttpClient())
        {
            using var sourceStream = client.GetStreamAsync("https://raw.githubusercontent.com/hdf5-benchmark/data/main/data/chunked_no_filter.h5");
            using var targetStream = new FileStream(fileName, FileMode.Create);

            sourceStream.Result.CopyTo(targetStream);
        }

        var file = H5File.OpenRead(fileName);
        _dataset = file.Dataset("chunked");
    }

    [Benchmark]
    public void Run()
    {
        _dataset.Read(buffer: _buffer);
    }
}