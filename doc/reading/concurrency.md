# Concurrency

Reading data from a dataset is thread-safe in the following cases, depending on the type of `H5File` constructor method you used:

|         | Open(`string`) | Open(`MemoryMappedViewAccessor`) | Open(`Stream`)                                        |
| ------- | -------------- | -------------------------------- | ----------------------------------------------------- |
| .NET 4+ | x              | ✓                               | x                                                     |
| .NET 6+ | ✓             | ✓                               | ✓ (if: `Stream` is `FileStream` or `AmazonS3Stream`) |

> The multi-threading support comes **without** significant usage of locking. Currently only the global heap cache uses thread synchronization primitives.

> [!WARNING]
> The default `SimpleReadingChunkCache` is not thread safe and therefore every read operation must use its own cache (which is the default). This will be solved in a future release.

## Multi-Threading (Memory-Mapped File)

If you have opened a file as memory-mapped file, you may read the data in parallel like this:

```cs
using System.IO.MemoryMappedFiles;

const ulong TOTAL_ELEMENT_COUNT = xxx;
const ulong SEGMENT_COUNT = xxx;
const ulong SEGMENT_SIZE = TOTAL_ELEMENT_COUNT / SEGMENT_COUNT;

using var mmf = MemoryMappedFile.CreateFromFile(FILE_PATH);
using var accessor = mmf.CreateViewAccessor();
using var file = H5File.Open(accessor);

var dataset = file.Dataset("xxx");
var buffer = new float[TOTAL_ELEMENT_COUNT];

Parallel.For(0, SEGMENT_COUNT, i =>
{
    var start = i * SEGMENT_SIZE;
    var partialBuffer = buffer.Slice(start, length: SEGMENT_SIZE);
    var fileSelection = new HyperslabSelection(start, block: SEGMENT_SIZE)

    dataset.Read<float>(partialBuffer, fileSelection);
});

```

## Multi-Threading (FileStream)

| Requires  |
| --------- |
| `.NET 6+` |

Starting with .NET 6, there is a new API to access files in a thread-safe way which PureHDF utilizes. The process to load data in parallel is similar to the memory-mapped file approach above:

```cs
const ulong TOTAL_ELEMENT_COUNT = xxx;
const ulong SEGMENT_COUNT = xxx;
const ulong SEGMENT_SIZE = TOTAL_ELEMENT_COUNT / SEGMENT_COUNT;

using var file = H5File.OpenRead(FILE_PATH);

var dataset = file.Dataset("xxx");
var buffer = new float[TOTAL_ELEMENT_COUNT];

Parallel.For(0, SEGMENT_COUNT, i =>
{
    var start = i * SEGMENT_SIZE;
    var partialBuffer = buffer.Slice(start, length: SEGMENT_SIZE);
    var fileSelection = new HyperslabSelection(start, block: SEGMENT_SIZE)

    dataset.Read<float>(partialBuffer, fileSelection);
});

```