# Reading

The following code snippets show how to work the reading API. The first step is to open the file to read from:

```cs
var file = H5File.OpenRead("path/to/file.h5");
```

The method `H5File.OpenRead` returns an instance of type `NativeFile` which represents the root group `/`. From there you can access any object within the file as shown below:

```cs
var group = file.Group("/path/to/my/group");
var dataset = file.Dataset("/path/to/my/dataset");
var commitedDataType = file.Group("/path/to/my/datatype");
var unknownObject = file.Get("/path/to/my/unknown/object");
```

The following example shows you how to iterate over the objects within an HDF5 group:

```cs
foreach (var link in group.Children())
{
    var message = link switch
    {
        IH5Group group               => $"I am a group and my name is '{group.Name}'.",
        IH5Dataset dataset           => $"I am a dataset, call me '{dataset.Name}'.",
        IH5CommitedDatatype datatype => $"I am the data type '{datatype.Name}'.",
        IH5UnresolvedLink lostLink   => $"I cannot find my link target =( shame on '{lostLink.Name}'."
        _                            => throw new Exception("Unknown link type")
    };

    Console.WriteLine(message);
}
```

> [!NOTE]
> An `IH5UnresolvedLink` becomes part of the `Children` collection when a symbolic link is dangling, i.e. the link target does not exist or cannot be accessed.

HDF5 objects can have zero or more attributes attached which can either be accessed by enumerating all attributes (`myObject.Attributes()`) or by direct access (`myObject.Attribute("attribute-name")`);

When you have a dataset or attribute available, you can read it's data by providing a compatible return type as shown below.

> [!NOTE]
> An overview over compatible return types can be found here in the [Simple Data](simple.md) or the [Complex Data](complex.md) sections.

```cs
var intScalar = dataset.Read<int>();
var doubleArray = dataset.Read<double[]>();
var double2DArray = dataset.Read<double[,]>();
var double3DArray = dataset.Read<double[,,]>();
var floatJaggedArray = dataset.Read<float[][]>(); /* This works only for variable length sequences */
```

# Unsupported Features

The following features are **not** (yet) supported:

- Filters
  - `N-bit`
  - `SZIP`
- Virtual datasets
  - with **unlimited dimensions**
- Data Types
  - Reference: `Attribute reference`, `object reference 2`, `dataset region reference 2` (I was unable to produce sample files using `h5py` or `HDF.PInvoke1.10` - the feature seems to be too new)