**Project is not yet released because support for reading partial datasets (hyperslabs) and support for virtual datasets and external files is still missing.**

# HDF5.NET

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/hdf5.net?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/hdf5-net) [![NuGet](https://img.shields.io/nuget/vpre/HDF5.NET.svg?label=Nuget)](https://www.nuget.org/packages/HDF5.NET)

A pure C# library that makes reading of HDF5 files (groups, datasets, attributes, links, ...) very easy.

The implemention follows the [HDF5 File Format Specification](https://support.hdfgroup.org/HDF5/doc/H5.format.html)

## 1. Getting Started

### HDF5 File and Groups

```cs

// open HDF5 file
using var root = H5File.Open(<TODO: improve signature>);

// get nested group
var group = root.Get<H5Group>("/my/nested/group");
```

### Datasets

```cs

// get dataset in group
var dataset = group.Get<H5Dataset>("mydataset");

// alternatively, use the full path
var dataset = group.Get<H5Dataset>("/my/nested/group/mydataset");

// read data
var data = dataset.Read<int>();
var stringData = dataset.ReadString(); // not yet implemented
var compoundData = dataset.ReadCompound<T>(); // not yet implemented
```

### Attributes

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

### Links
```cs
// hard link, soft link or external file link
var link = root.GetSymbolicLink("mySymbolicLink");
```

## 2. Filters

### Built-in filters
- Shuffle
- Fletcher32
- Deflate (zlib)

### External filters
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

### Tested external filters
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

## 3. Advanced Scenarios

### Reading compound data

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

##### Structs with nullable fields

If have a struct with string fields, you need to use the slower `ReadCompound` method:

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
```

Please note that in this case no special attributes are required at the expense of lower performance.