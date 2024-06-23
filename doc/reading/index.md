# Reading

The following code snippets show how to work with the reading API. The first step is to open the file to read from:

```cs
using PureHDF;

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
        IH5Group childGroup                 => $"I am a group and my name is '{childGroup.Name}'.",
        IH5Dataset childDataset             => $"I am a dataset, call me '{childDataset.Name}'.",
        IH5CommitedDatatype childDatatype   => $"I am the data type '{childDatatype.Name}'.",
        IH5UnresolvedLink lostLink          => $"I cannot find my link target =( shame on '{lostLink.Name}'.",
        _                                   => throw new Exception("Unknown link type")
    };

    Console.WriteLine(message);
}
```

> [!NOTE]
> An `IH5UnresolvedLink` becomes part of the `Children` collection when a symbolic link is dangling, i.e. the link target does not exist or cannot be accessed.

HDF5 objects can have zero or more attributes attached which can either be accessed by enumerating all attributes (`myObject.Attributes()`) or by direct access (`myObject.Attribute("attribute-name")`);

When you have a dataset or attribute available, you can read it's data by providing a compatible generic type as shown below.

```cs
var intScalar = dataset.Read<int>();
var doubleArray = dataset.Read<double[]>();
var double2DArray = dataset.Read<double[,]>();
var double3DArray = dataset.Read<double[,,]>();
var floatJaggedArray = dataset.Read<float[][]>(); /* This works only for variable length sequences */
```

> [!NOTE]
> An overview over compatible generic types can be found here in the [Simple Data](simple.md) or the [Compound Data](compound.md) sections.