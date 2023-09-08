# Writing

The first step is to create a new `H5File` instance:

```cs
var file = new H5File();
```

A `H5File` derives from the `H5Group` type because it represents the root group. `H5Group` implements the `IDictionary` interface, where the keys represent the links in an HDF5 file and the value determines the type of the link: either it is another `H5Group` or a `H5Dataset`. 

You can create an empty group like this:

```cs
var group = new H5Group();
```

If the group should have some datasets, just add them using the dictionary collection initializer - just like with a normal dictionary:

```cs
var group = new H5Group()
{
    ["numerical-dataset"] = new double[] { 2.0, 3.1, 4.2 },
    ["string-dataset"] = new string[] { "One", "Two", "Three" }
}
```

Datasets and attributes can both be created either by instantiating their specific class (`H5Dataset`, `H5Attribute`) or by just providing some kind of data. This data can be nearly anything: arrays, scalars, numerical values, strings, anonymous types, enums, complex objects, structs, bool values, etc. However, whenever you want to provide more details like the dimensionality of the attribute or dataset, the chunk layout or the filters to be applied to a dataset, you need to instantiate the appropriate class.

But first, let's see how to add attributes. Attributes cannot be added directly using the dictionary collection initializer because that is only for datasets. However, every `H5Group` has an `Attribute` property which accepts our attributes:

```cs
var group = new H5Group()
{
    Attributes = new()
    {
        ["numerical-attribute"] = new double[] { 2.0, 3.1, 4.2 },
        ["string-attribute"] = new string[] { "One", "Two", "Three" }
    }
}
```

The full example with the root group, a subgroup, two datasets and two attributes looks like this:

```cs
var file = new H5File()
{
    ["my-group"] = new H5Group()
    {
        ["numerical-dataset"] = new double[] { 2.0, 3.1, 4.2 },
        ["string-dataset"] = new string[] { "One", "Two", "Three" },
        Attributes = new()
        {
            ["numerical-attribute"] = new double[] { 2.0, 3.1, 4.2 },
            ["string-attribute"] = new string[] { "One", "Two", "Three" }
        }
    }
};
```

The last step is to write the defined file to the drive:

```cs
file.Write("path/to/file.h5");
```

## Dimensions

If you do not specify the dimensions, they will be derived from the data being provided. It the data is scalar, it will also be a scalar in the HDF5 file. If the data is array-like (e.g. `int[,]`), the dimensionality of that array is determined and will be used for the dimensionality of the dataset in the file. You can explicitly specify the dimensionality (or `reshape`) like this:

```cs
// Create a 100x100 dataset - the data itself must be an
// array-like type with a total of 100x100 = 10.000 elements. */
var dataset = new H5Dataset(data, fileDims: new ulong[] { 100, 100 });
```

Here is a quick example how to use a multidimensional array to determine the shape of the dataset:

```cs
// Create a 3x3 dataset - no `fileDims` parameter required
var data = new int[,] 
{
    { 0, 1, 2 },
    { 3, 4, 5 },
    { 6, 7, 8 }
};

var dataset = new H5Dataset(data);
```

## Chunks

To chunk data, give the `H5Dataset` some chunk dimensions:

```cs
var dataset = new H5Dataset(data, chunks: new ulong[] { 10, 10 });
```

When no chunks and no filters are specified, the data will be written as compact or contiguous datasets, depending on the total size.

## Filters

Many of the standard HDF5 filters are supported. Please see the [Filters](filters.md) section for more details about the available filters.

Filters can be configured either for all datasets or for a specific dataset.

If you like to configure them for all datasets, create an instance of the `H5WriteOptions` class and add some filters to the pipeline:

```cs
var options = new H5WriteOptions(
    Filters: new() {
        ShuffleFilter.Id,
        DeflateFilter.Id
    }
);
```

Then pass the options to the `H5File.Write` method:

```cs
file.Write("path/to/file.h5", options);
```

Use the filters parameter to configure the filter pipeline for a single dataset:

```cs
var datasetCreation = new H5DatasetCreation(Filters: new() {
    ShuffleFilter.Id,
    DeflateFilter.Id
})

var dataset = new H5Dataset(..., datasetCreation: datasetCreation);
```

## TODO
- deferred writing
- slicing