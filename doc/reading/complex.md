
# Complex Data

There are three ways to read compound data which are explained in the following sections. Here is an overview:

| method                              | constraint | speed  | requirements                                               |
| ----------------------------------- | ---------- | ------ | ---------------------------------------------------------- |
| `Read<T>()`                         | unmanaged  | fast   | predefined type required with correct field offsets        |
| `Read<T>()`                         | -          | medium | predefined type required, support for variable-length data |
| `Read<Dictionary<string, object>()` | -          | slow   | -                                                          |

## Compounds without reference type data

Compound data without string-like or array-like members can be read like any other dataset using a high performance copy operation. To do so, define a .NET struct and specify the field offsets using the `StructLayout` and `FieldOffset` attributes:

```cs
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit, Size = 5)]
struct SimpleStruct
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

> [!WARNING]
> Make sure the field offset attributes match the field offsets defined in the HDF5 file when the dataset was created.

> [!NOTE]
> This method does not require that the structs field names match since they are simply mapped by their offset.

If the compound contains fixed size array data (here: `3`), you would need to add the `unsafe` modifier to the struct definition and define the corresponding struct field as follows:


```cs
[StructLayout(LayoutKind.Explicit, Size = 8)]
unsafe struct SimpleStructWithArray
{
    // ... all the fields from the struct above, plus:

    [FieldOffset(5)]
    public fixed float FloatArray[3];
}

var compoundData = dataset.Read<SimpleStruct>();
```

## Compounds with reference types (strings, arrays)

If compound has members of string-like or array-like type, the read operation will still work but a slower code path will be invoked to properly decode the variable-length data.

```cs
struct NullableStruct
{
    public float FloatValue;
    public string StringValue1;
    public string StringValue2;
    public byte ByteValue;
    public short ShortValue;
    public float[] FloatArray;
}

var compoundData = dataset.Read<NullableStruct>();
```

> [!NOTE]
> For compounds with reference type data, it is mandatory that the field names match exactly those in the HDF5 file. If you would like to use custom field names, consider the approach shown below.

```cs

// Apply the H5NameAttribute to the field with custom name.
struct NullableStructWithCustomFieldName
{
    [H5Name("FloatValue")]
    public float FloatValueWithCustomName;

    // ... more fields
}

// Create a name translator.
using System.Reflection;

Func<FieldInfo, string?> converter = fieldInfo =>
{
    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
    return attribute is not null ? attribute.Name : null;
};

// Use that name translator.
var options = new H5ReadOptions() { FieldNameMapper = converter };
var h5file = H5File.OpenRead(..., options);
var dataset = h5file.Dataset(...);
var compoundData = dataset.Read<NullableStructWithCustomFieldName>();
```

## Class vs. struct

You may want to use a class with a specific set of properties and use that as return type for complex data. For classes properties instead of fields will be considered by default - and your need to make sure that your class has a parameterless constructor. Additionally, for classes, the field names must always match (or use a property name mapper as shown above for fields).

You can use the `H5ReadOptions` to change the default behavior for structs and classes. These options can be passed to the `H5File.OpenRead(...)` method.

## Unknown compounds

You have no idea how the compound in the HDF5 file looks like? Or it is so large that it is no fun to predefine a struct or class for it? In that case, you can simply call `dataset.Read<Dictionary<string, object>>()` where the values of the returned dictionary can be anything from simple value types to arrays or nested dictionaries (or even `NativeObjectReference1`), depending on the kind of data in the file. Use the standard .NET dictionary methods to work with these kind of data.

The type mapping is as follows:

| H5 type                        | .NET type                    |
| ------------------------------ | ---------------------------- |
| fixed point, 1 byte,  unsigned | `byte`                       |
| fixed point, 1 byte,    signed | `sbyte`                      |
| fixed point, 2 bytes, unsigned | `ushort`                     |
| fixed point, 2 bytes,   signed | `short`                      |
| fixed point, 4 bytes, unsigned | `uint`                       |
| fixed point, 4 bytes,   signed | `int`                        |
| fixed point, 8 bytes, unsigned | `ulong`                      |
| fixed point, 8 bytes,   signed | `long`                       |
| floating point, 4 bytes        | `float `                     |
| floating point, 8 bytes,       | `double`                     |
| string                         | `string`                     |
| bitfield                       | `byte[]`                     |
| opaque                         | `byte[]`                     |
| compound                       | `Dictionary<string, object>` |
| reference                      | `NativeObjectReference1`     |
| enumerated                     | `<base type>`                |
| variable length string         | `string`                     |
| variable length sequence       | `<base type>[]`              |
| 1D array                       | `<base type>[]`              |
| 2D array                       | `<base type>[,]`             |
| ND array                       | `<base type>[...]`           |

Not supported data types like `time` will be represented as `null`.