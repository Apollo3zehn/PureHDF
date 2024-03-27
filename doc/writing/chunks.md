# Chunks

To chunk data, give the `H5Dataset` some chunk dimensions:

```cs
var dataset = new H5Dataset(
    data, 
    chunks: [10, 10]);
```

When no chunks and no filters are specified, the data will be written as compact or contiguous datasets, depending on the total size.