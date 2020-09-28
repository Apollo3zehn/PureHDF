**Project is not yet released because support for reading partial datasets (hyperslabs) and support for virtual datasets still missing.**

# HDF5.NET

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/hdf5.net?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/hdf5-net) [![NuGet](https://img.shields.io/nuget/vpre/HDF5.NET.svg?label=Nuget)](https://www.nuget.org/packages/HDF5.NET)

A pure C# library that makes reading of HDF5 files (groups, datasets, attributes, links, ...) very easy.

The implemention follows the [HDF5 File Format Specification](https://support.hdfgroup.org/HDF5/doc/H5.format.html)

## 1. Links

### File and Groups

```cs

// open HDF5 file, it represents the root group ('/')
using var root = H5File.Open(<TODO: improve signature>);

// get nested group
var group = root.GetGroup("/my/nested/group");
```

### Datasets

```cs

// get dataset in group
var dataset = group.GetDataset("myDataset");

// alternatively, use the full path
var dataset = group.GetDataset("/my/nested/group/myDataset");

// read data
var data = dataset.Read<int>();
var stringData = dataset.ReadString(); // not yet implemented
var compoundData = dataset.ReadCompound<T>(); // not yet implemented

// read enums
enum MyEnum : short /* just make sure the HDF enum is based on the same type */
{
    MyValue1 = 1,
    MyValue2 = 2,
    MyValue3 = 3
}

var data = dataset.Read<MyEnum>();
```

### Commited Data Types

```cs
var commitedDataType = group.GetCommitedDataType("myCommitedDataType");
```

### Unknown Link Type
If you do not know what kind of link to expect, use the following code:

```cs
var link = group.Get("/path/to/unknown/link");
```

### External Links

If an external link points to a relative file path it might be necessary to provide a file prefix (see also this [overview](https://support.hdfgroup.org/HDF5/doc/RM/H5L/H5Lcreate_external.htm)).

You can either set an environment variable:

```cs
 Environment.SetEnvironmentVariable("HDF5_EXT_PREFIX", "/my/prefix/path");
```

Or you can pass the prefix as an overload parameter to one of the different `Get` methods:

```cs
var linkAccess = new H5LinkAccessPropertyList() 
{
    ExternalFilePrefix = prefix 
}

var dataset = root.GetDataset(path, linkAccess);
```

### Iteration

```cs
foreach (var link in group.Children)
{
    var message = link switch
    {
        H5Group group               => $"I am a group and my name is '{group.Name}'.",
        H5Dataset dataset           => $"I am a dataset, call me '{dataset.Name}'.",
        H5CommitedDataType dataType => $"I am the data type '{dataType.Name}'.",
        H5UnresolvedLink lostLink   => $"I cannot find my link target =( shame on '{lostLink.Name}'."
        _                           => throw new Exception("Unknown link type");
    }

    Console.WriteLine(message)
}
```

An `H5UnresolvedLink` becomes part of the `Children` collection when a symbolic link is dangling, i.e. the link target does not exist or cannot be accessed. Section [Accessing Symbolic Links](#Accessing-Symbolic-Links) describes how to get the symbolic link itself instead of its target.

## 2. Attributes

```cs
// get attribute of group
var attribute = group.GetAttribute("myAttributeOnAGroup");

// get attribute of dataset
var attribute = dataset.GetAttribute("myAttributeOnADataset");

// read data
var data = attribute.Read<int>();
var stringData = attribute.ReadString();
var compoundData = attribute.ReadCompound<T>();
```
For more information on compound data, see section [Reading compound data](#Reading-compound-data).

## 3. Filters

### Built-in Filters
- Shuffle (hardware accelerated, SSE2/AVX2)
- Fletcher32
- Deflate (zlib)

### External Filters
Before you can use external filters, you need to register them using ```H5Filter.Register(...)```. This method accepts a filter identifier, a filter name and the actual filter function.

This function could look like the following and should be adapted to your specific filter library:

```cs
public static Memory<byte> FilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer)
{
    // Decompressing
    if (flags.HasFlag(ExtendedFilterFlags.Reverse))
    {
        // pseudo code
        byte[] decompressedData = MyFilter.Decompress(parameters, buffer.Span);
        return decompressedData;
    }
    // Compressing
    else
    {
        throw new Exception("Writing data chunks is not yet supported by HDF5.NET.");
    }
}

```

### Tested External Filters
- c-blosc2 (using [Blosc2.PInvoke](blosc2.pinvoke))

### How to use Blosc / Blosc2
(1) Install the P/Invoke package:

`dotnet package add Blosc2.PInvoke`

(2) Add the Blosc filter registration [helper function](https://github.com/Apollo3zehn/HDF5.NET/blob/dev/tests/HDF5.NET.Tests/BloscHelper.cs#L9-L75) to your code.

(3) Register Blosc:

```cs
 H5Filter.Register(
     identifier: (FilterIdentifier)32001, 
     name: "blosc2", 
     filterFunc: BloscHelper.FilterFunc);
```

## 4. Advanced Scenarios

### Reading Multidimensional Data

Sometimes you want to read the data as multidimensional arrays. In that case use one of the `byte[]` overloads like `ToArray3D` (there are overloads up to 6D). Here is an example:

```cs
var data3D = dataset
    .Read<int>()
    .ToArray3D(new long[] { -1, 7, 2 });
```

The methods accepts a `long[]` with the new array dimensions. This feature works similar to Matlab's [reshape](https://de.mathworks.com/help/matlab/ref/reshape.html) function. A slightly adapted citation explains the behavior:
> When you use `-1` to automatically calculate a dimension size, the dimensions that you *do* explicitly specify must divide evenly into the number of elements in the input array.

### Reading Compound Data

##### Structs without nullable fields

Structs without any nullable fields (i.e. no strings and other reference types) can be read like any other dataset using a high performance copy operation:

```cs
[StructLayout(LayoutKind.Explicit, Size = 5)]
public struct SimpleStruct
{
    [FieldOffset(0)]
    public byte ByteValue;

    [FieldOffset(1)]
    public ushort UShortValue;

    [FieldOffset(3)]
    public TestEnum EnumValue;
}

var compoundData = dataset.Read<SimpleStruct>();
```

Just make sure the field offset attributes matches the field offsets defined in the HDF5 file when the dataset was created.

*This method does not require that the structs field names match since they are simply mapped by their offset.*

##### Structs with nullable fields

If you have a struct with string fields, you need to use the slower `ReadCompound` method:

```cs
public struct NullableStruct
{
    public float FloatValue;
    public string StringValue1;
    public string StringValue2;
    public byte ByteValue;
    public short ShortValue;
}

var compoundData = dataset.ReadCompound<NullableStruct>();
var compoundData = attribute.ReadCompound<NullableStruct>();
```

*This method requires no special attributes but it is mandatory that the field names match exactly to those in the HDF5 file. If you would like to use custom field names, consider the following solution:*

```cs

// Apply the H5NameAttribute to the field with custom name.
public struct NullableStructWithCustomFieldName
{
    [H5Name("FloatValue")]
    public float FloatValueWithCustomName;

    // ... more fields
}

// Create a name translator.
Func<FieldInfo, string> converter = fieldInfo =>
{
    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
    return attribute != null ? attribute.Name : fieldInfo.Name;
};

// Use that name translator.
var compoundData = dataset.ReadCompound<NullableStructWithCustomFieldName>(converter);
```

### Accessing Symbolic Links

If you do not want the library to transparently follow a link but instead get the link itself, use the following:

```cs
// hard link, soft link or external file link
var link = root.GetSymbolicLink("mySymbolicLink");
```