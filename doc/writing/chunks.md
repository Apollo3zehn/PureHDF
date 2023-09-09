# Chunks

To chunk data, give the `H5Dataset` some chunk dimensions:

```cs
var dataset = new H5Dataset(
    data, 
    chunks: new uint[] { 10, 10 });
```

When no chunks and no filters are specified, the data will be written as compact or contiguous datasets, depending on the total size.

> [!WARNING]
> Chunks can only be written to file once! Depending on your dataset and chunk dimensions, chunks are being accessed more than once during a write operation (only relevant for multidimensional arrays). To avoid an exception, make sure the chunk cache is large enough so that chunks are not being flushed from the cache to disk too early. You can adjust the chunk cache parameters by passing `H5DatasetAccess` properties to the dataset's constructor as shown below.

```cs
using PureHDF.VOL.Native;

var datasetCreation = new H5DatasetCreation(ChunkCache: new SimpleChunkCache(...));

var dataset = new H5Dataset(
    data, 
    chunks: new uint[] { 10, 10 },
    datasetCreation: datasetCreation
);
```