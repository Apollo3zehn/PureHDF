# Concurrency

Reading data from a dataset is thread-safe in the following cases, depending on which `H5File.Open` overload you used:

| Overload | Concurrent reads |
| -------- | ---------------- |
| `Open(string)` / `OpenRead(string)` | ✓ |
| `Open(MemoryMappedViewAccessor)` | ✓ |
| `Open(ReadOnlyMemory<byte>)` | ✓ |
| `Open(IConcurrentStream)` | ✓ |
| `Open(Stream)` | ✓ only if the stream is a `FileStream`; otherwise ✗ |

A per-operation driver is allocated for every read, so a dataset or attribute resolved once can be read concurrently through a single `H5File`. Object navigation (e.g. `file.Dataset("x")`, attribute enumeration) still moves the file-level cursor and remains single-threaded — resolve first, then read in parallel.

> [!WARNING]
> The `Open(Stream)` overload is only thread-safe when the stream is a `FileStream`: PureHDF unwraps a `FileStream` to a handle driver that reads positionally (`RandomAccess.Read`), so each operation carries its own position and never touches the stream's cursor. Any other `Stream` subclass is driven through a shared cursor, so concurrent reads through it silently corrupt data. To read a non-`FileStream` remote source concurrently, implement `IConcurrentStream` and use `Open(IConcurrentStream)` instead.

> The multi-threading support comes **without** significant usage of locking. Only the global heap cache and the chunk cache use thread synchronization primitives.

> [!NOTE]
> The default `SimpleReadingChunkCache` is thread safe and may be shared across concurrent reads by passing one instance via `H5DatasetAccess.ChunkCache`. The default path builds a fresh cache per read, so the lock only matters when you opt into sharing — which is the only reason to pass a cache in the first place, since it is what makes repeated reads of the same chunks cheap.

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

## Multi-Threading (In-Memory Buffer)

When the file is already in memory — for example, downloaded from a remote store or decompressed in place — pass the buffer directly. Reads are pure span slices over the same buffer and never suspend, so no async entry point is needed:

```cs
using var file = H5File.Open(sourceBuffer);

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

The caller owns `sourceBuffer`; the driver never writes to it. Do not mutate the buffer while the `H5File` is open.