# Filters

Many of the standard HDF5 filters are supported. Please see the [Filters](../filters.md) section for more details about the available filters.

Filters can be configured either for all datasets or for a specific dataset. If you like to configure them for all datasets, create an instance of the `H5WriteOptions` and add some filters to the pipeline:

```cs
using PureHDF.Filters;

var options = new H5WriteOptions(
    Filters:
    [
        ShuffleFilter.Id,
        DeflateFilter.Id
    ]
);
```

Then pass the options to the `H5File.Write` method:

```cs
file.Write("path/to/file.h5", options);
```

An alternative is to configure the filter pipeline for a single dataset:

```cs
var datasetCreation = new H5DatasetCreation(
    Filters:
    [
        ShuffleFilter.Id,
        DeflateFilter.Id
    ]
)

var dataset = new H5Dataset(..., datasetCreation: datasetCreation);
```