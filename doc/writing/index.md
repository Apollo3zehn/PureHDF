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

> [!NOTE]
> Please refer to [data types](data-types.md) for more information about how to write special data types.

## Metadata placement

By default the writer allocates file structure - object headers, chunk indexes, global heap collections - in the order it encodes it, so structure ends up spread evenly through the file alongside the data.

That costs nothing locally, but it is expensive for a reader that fetches ranges rather than seeking freely, typically over HTTP. Such a reader has to see every byte of structure to walk the file, so structure in every range means downloading every range. `H5WriteOptions.MetadataPlacement` offers two alternatives:

```cs
var options = new H5WriteOptions
{
    MetadataPlacement = H5MetadataPlacement.FrontLoaded
};

file.Write("path/to/file.h5", options);
```

| Placement | Allocation | Effect |
|---|---|---|
| `Interleaved` | in encode order | the default; adds nothing to a write |
| `Aggregated` | from blocks that double in size, forming a few clusters | needs no estimate of the total; costs a proportion of the metadata, not a fixed block |
| `FrontLoaded` | from one region reserved at the front | a reader fetches all structure in one range |

Measured over a stream counting what an HTTP range client would transfer, walking a file of 600 groups of deflated measurement series at 256 kB ranges:

```
interleaved    fetched 4,050,543 B of 4,050,543 B   100.0% of file   16 requests
aggregated     fetched   786,432 B of 4,052,705 B    19.4% of file    3 requests
front-loaded   fetched   262,144 B of 4,062,879 B     6.5% of file    1 request
```

Front loading always ends at a single request, since the reservation is one contiguous span. The interleaved cost is the whole file however large it grows, so the gap widens with size.

`FrontLoaded` sizes its reservation by measuring rather than estimating: the writer encodes the file once against a stream that discards everything and reads the total off its allocator. The pass does not compress and, for fixed-size data, does not touch the data at all, so it costs a fraction of the write it precedes. Set `MetadataReservation` to a byte count to skip the pass - and do set it when writing variable-length data through `BeginWrite`, since how much global heap such data needs follows from values the pass has not seen yet.

A reservation that turns out too small spills into blocks, so it loses locality rather than failing. All three placements produce valid HDF5 - the format imposes no ordering - and the choice costs nothing on read for a local file.

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

## Soft Links

You can create soft links via the `H5SoftLink` type:

```cs
var file = new H5File
{
    ["group_1"] = new H5Group
    {
        ["dataset"] = new int[] { 1, 2, 3 }
    },

    ["group_2"] = new H5Group
    {
        /* soft link to a group */
        ["soft_link_1"] = new H5SoftLink("/group_1"),

        /* soft link to a dataset */
        ["soft_link_2"] = new H5SoftLink("/group_1/dataset")
    }
};
```