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

**Dimensions**
If you do not specify the dimensions, they will be derived from the data being provided. It the data is scalar, it will also be a scalar in the HDF5 file. If the data is array-like (e.g. `int[,]`), the dimensionality of that array is determined and will be used for the dimensionality of the dataset in the file. You can explicitly specify the dimensionality like this:

```cs
var dataset = new H5Dataset(data, fileDims: new ulong[] { 100, 100 });
```

**Chunks**

To chunk data, give the `H5Dataset` some chunk dimensions:

```cs
var dataset = new H5Dataset(data, chunks: new ulong[] { 10, 10 });
```

**Filters**

Filters can be configure either for all datasets or for a specific dataset.

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

**External filters**

External filters need to be registered first as shown in the documentation for the reading API.

# TODO
- deferred writing
- slicing