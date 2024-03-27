# Writing

PureHDF can easily create new files, as described in more detail below. However, **editing existing** files is outside the scope of PureHDF.

To get started, first create a new `H5File` instance:

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
};
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
};
```

The full example with the root group, a subgroup, two datasets and two attributes looks like this:

```cs
using PureHDF;

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

## Deferred writing

You may want to write data at a later point in time (for instance if the data is not available yet) and for this scenario PureHDF offers a slightly different API. The following example shows how to use the `H5File.BeginWrite(...)` method to get a writer which allows you to write data to the dataset one or multiple times until the writer instance is being disposed.

```cs
var data = Enumerable.Range(0, 100).ToArray();
var dataset = new H5Dataset<int[]>(fileDims: [(ulong)data.Length]);

var file = new H5File
{
    ["my-dataset"] = dataset
};

using var writer = file.BeginWrite("path/to/file.h5");
writer.Write(dataset, data);
```

You probably do not want to write all data at once but in chunks. To do so, make use of selections to select the proper slice of the dataset to write to. Create a selection and then pass it to the write method (the element count of the selection must match the element count of the data):

```cs
using PureHDF.Selections;

var fileSelection = new HyperslabSelection(start: 2, block: 5);
writer.Write(dataset, data, fileSelection: fileSelection);
```

> [!NOTE]
> See [slicing](../reading/slicing.md) for information about available selections.