**Project is not yet released because support for reading partial datasets (hyperslabs), virtual datasets and region references (IV.B. Disk Format: Level 2B) is still missing.**

# HDF5.NET

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/hdf5.net?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/hdf5-net) [![NuGet](https://img.shields.io/nuget/vpre/HDF5.NET.svg?label=Nuget)](https://www.nuget.org/packages/HDF5.NET)

A pure C# library that makes reading of HDF5 files (groups, datasets, attributes, links, ...) very easy.

The implemention follows the [HDF5 File Format Specification](https://support.hdfgroup.org/HDF5/doc/H5.format.html)

## 1. Objects

```cs
// open HDF5 file, the returned H5File instance represents the root group ('/')
using var root = H5File.Open(<TODO: improve signature>);
```

### 1.1 Get Object
#### Group

```cs
// get nested group
var group = root.Group("/my/nested/group");
```

#### Dataset

```cs

// get dataset in group
var dataset = group.Dataset("myDataset");

// alternatively, use the full path
var dataset = group.Dataset("/my/nested/group/myDataset");
```

#### Commited Data Type

```cs
// get commited data type in group
var commitedDatatype = group.CommitedDatatype("myCommitedDatatype");
```

#### Any Object Type
When you do not know what kind of link to expect at a given path, use the following code:

```cs
// get H5Object (base class of all HDF5 object types)
var myH5Object = group.Get("/path/to/unknown/object");
```

### 1.2 Additional Info
#### External File Link

With an external link pointing to a relative file path it might be necessary to provide a file prefix (see also this [overview](https://support.hdfgroup.org/HDF5/doc/RM/H5L/H5Lcreate_external.htm)).

You can either set an environment variable:

```cs
 Environment.SetEnvironmentVariable("HDF5_EXT_PREFIX", "/my/prefix/path");
```

Or you can pass the prefix as an overload parameter:

```cs
var linkAccess = new H5LinkAccess() 
{
    ExternalLinkPrefix = prefix 
}

var dataset = group.Dataset(path, linkAccess);
```

#### Iteration

Iterate through all link in a group:

```cs
foreach (var link in group.Children)
{
    var message = link switch
    {
        H5Group group               => $"I am a group and my name is '{group.Name}'.",
        H5Dataset dataset           => $"I am a dataset, call me '{dataset.Name}'.",
        H5CommitedDatatype datatype => $"I am the data type '{datatype.Name}'.",
        H5UnresolvedLink lostLink   => $"I cannot find my link target =( shame on '{lostLink.Name}'."
        _                           => throw new Exception("Unknown link type");
    }

    Console.WriteLine(message)
}
```

An `H5UnresolvedLink` becomes part of the `Children` collection when a symbolic link is dangling, i.e. the link target does not exist or cannot be accessed.

## 2. Attributes

```cs
// get attribute of group
var attribute = group.Attribute("myAttributeOnAGroup");

// get attribute of dataset
var attribute = dataset.Attribute("myAttributeOnADataset");
```

## 3. Data

The following code samples work for datasets as well as attributes.

```cs
// class: fixed-point

    var data = dataset.Read<int>();

// class: floating-point

    var data = dataset.Read<double>();

// class: string

    var data = dataset.ReadString();

// class: bitfield

    [Flags]
    enum SystemStatus : ushort /* make sure the enum in HDF file is based on the same type */
    {
        MainValve_Open          = 0x0001
        AuxValve_1_Open         = 0x0002
        AuxValve_2_Open         = 0x0004
        MainEngine_Ready        = 0x0008
        FallbackEngine_Ready    = 0x0010
        // ...
    }

    var data = dataset.Read<SystemStatus>();
    var readyToLaunch = data[0].HasFlag(SystemStatus.MainValve_Open | SystemStatus.MainEngine_Ready);

// class: opaque

    var data = dataset.Read<byte>();
    var data = dataset.Read<MyOpaqueStruct>();

// class: compound

    /* option 1 (faster) */
    var data = dataset.Read<MyNonNullableStruct>();
    /* option 2 (slower, for more info see the link below after this code block) */
    var data = dataset.ReadCompound<MyNullableStruct>();

// class: reference

    var data = dataset.Read<H5ObjectReference>();
    var firstRef = data.First();

    /* NOTE: Dereferencing would be quite fast if the object's name
     * was known. Instead, the library searches recursively for the  
     * object. Do not dereference using a parent (group) that contains
     * any circular soft links. Hard links are no problem.
     */

    /* option 1 (faster) */
    var firstObject = directParent.Get(firstRef);

    /* option 1 (slower, use if you don't know the objects parent) */
    var firstObject = root.Get(firstRef);

// class: enumerated

    enum MyEnum : short /* make sure the enum in HDF file is based on the same type */
    {
        MyValue1 = 1,
        MyValue2 = 2,
        // ...
    }

    var data = dataset.Read<MyEnum>();

// class: variable length

    var data = dataset.ReadString();

// class: array

    var data = dataset
        .Read<int>()
        /* dataset dims = int[2, 3] */
        /*   array dims = int[4, 5] */
        .ToArray4D(2, 3, 4, 5);

// class: time
// -> not supported (reason: the HDF5 C lib itself does not fully support H5T_TIME)
```

For more information on compound data, see section [Reading compound data](#Reading-compound-data).

## 4. Filters

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

## 5. Advanced Scenarios

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