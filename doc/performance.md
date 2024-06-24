# Performance

## Memory Management

PureHDF allows you to provide your own buffer so you can using for example the .NET `MemoryPool` to rent a buffer:

```cs
var dataset = (NativeDataset)file.Dataset("/the/dataset");
var length = (int)dataset.Space.Dimensions[0] * sizeof(double);

using var memoryOwner = MemoryPool<byte>.Shared.Rent(minBufferSize: length);
var memory = memoryOwner.Memory.Slice(0, length);

dataset.Read(buffer: memory);

var doubleData = MemoryMarshal.Cast<byte, double>(memory.Span);
```

Supported buffer types are `T` (in case of `dataset.Read<T>(...)`), `byte[]`, `Memory<byte>` and `Span<byte>`.

## Chunking

The HDF5 file format is a good choice for storing compressed data in chunks, which can actually improve speed by reducing the amount of data that needs to be read from disk. However, chunked data can only be accessed as a whole (due to compression), so it is important to choose an appropriate chunk size.

For example, consider a two-dimensional data set with dimensions of `10.000 x 100`. This dataset will be filled with real-time sampled data where the first dimension is the time axis, i.e. there will be `100` values per sample. You could now choose a chunk size of `1 x 100`, which means that the chunk is the size of a single sample. This chunk size works well for write operations. 

But when the measurement is finished, you want to read the data back into memory. Often the access pattern is different: instead of writing the data row by row, you now want to read the data column by column. This is often the case with measurement systems where you have tens or hundreds of individual channels. In our example, we want to read the first column - the first channel - with dimensions `10.000 x 1`. 

To do this, PureHDF has to open all the `10.000` chunks, decompress them, extract the first values and collect them in the final array. This will severely degrade performance. If the chunk size had been set to `10.000 x 1` instead, it would have been a single read operation and much faster overall. At the same time, however, the write performance is greatly reduced, because now `100` individual chunks have to be accessed per sample, where *access* means `decompress -> append new value -> compress`.

The best solution to this problem is to use chunk caches. A chunk cache holds the decompressed data, so in case of a write operation the pattern `decompress -> append new value -> compress` changes to `find chunk in cache -> append new value`, which is generally much faster. 

Performance will be degraded again if the chunk cache is too small. The default *reading* chunk cache properties are

- `521` chunk entries 
- with a maximum total size of `1 MB`

## Reading

The default implementation of the `IReadingChunkCache` interface is the  `SimpleReadingChunkCache`. You can change the parameters of this cache or replace it entirely with your own implementation as follows:

```cs
var dataset = (NativeDataset)file.Dataset("/the/dataset");

var datasetAccess = new H5DatasetAccess(
    ChunkCache: new SimpleReadingChunkCache(
        chunkSlotCount: 521, 
        byteCount: 1 * 1024 * 1024
    )
)

dataset.Read<T>(datasetAccess, ...);
```

## Writing

The default implementation of the `IWritingChunkCache` interface is the  `SimpleWritingChunkCache` which has no chunk count or chunk size limits because a current PureHDF limitation is that chunks can only be written once. Therefore the chunk cache **must** hold all data in memory until all other file structures are written.

If you want to use your own implementation of `IWritingChunkCache`, you can provide it in the `H5Dataset` constructor like this:

```cs
var datasetCreation = new H5DatasetCreation(
    ChunkCache: <your own chunk cache>
)

var dataset = new H5Dataset(..., datasetCreation: datasetCreation);
```

> [!NOTE]
> Alternatively, you can provide the chunk caches in a central place: `ChunkCache.DefaultReadingChunkCacheFactory` for reading, or `ChunkCache.DefaultWritingChunkCacheFactory` for writing.