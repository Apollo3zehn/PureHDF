Most data can simply be added to the `H5File` or `H5Group` dictionary like this:

```
var file = new H5File()
{
    ["my-dataset"] = data // data contains the simple or complex data you want to write
};
```

However, there are a few cases where a little bit more work is required. These are described in the following sections.

# Object References

Use the `H5ObjectReference` type to create object references. Variables of type `H5Dataset` can be implicitly casted to `H5ObjectReference`, i.e. no explicit casting is required. Here is an example:

```
var dataset1 = new H5Dataset(data: 1);
var dataset2 = new H5Dataset(data: 2);

var file = new H5File
{
    ["data1"] = dataset1,
    ["data2"] = dataset2,
    ["reference"] = new H5ObjectReference[] { dataset1, dataset2 }
};
```

# Nullable Value Types

To write nullable value type data use the generic version of `H5Dataset` (or `H5Attribute`). If you use the non-generic one instead, you will get a compiler warning. It is necessary to use the generic `H5Dataset<T>` in combination with `null` data because `null` looses all type information at runtime and then PureHDF does not know how to encode it. The generic type parameter keeps that type information.

Example:

```
int? value = 1; // or null

var file = new H5File()
{
    ["my-dataset"] = new H5Dataset<int?>(value)
};
```

As there is no native support for nullable values types in the HDF5 file format, here is some technical background about the workaround PureHDF uses:

Nullable value types (e.g. `int?`) require special handling within the HDF5 file because we need a way to store the possible `null` value. The HDF5 `variable-length sequence` data type is a good, though not perfect solution, for this problem if we only use the first element of the sequence. Since a variable-length sequence has no predefined length, a reference (or pointer) is stored in the dataset itself. And this reference points to the global heap where the actual data lives. In case this reference consists only of zeros, it means that the value is undefined (= `null`). 

Summary: To represent nullable value types, we use the HDF5 internal variable-length sequence data type with a sequence length of `1`.

```
var file = new H5File
{
    ["opaque"] = new H5Dataset(data, opaqueInfo: opaqueInfo)
};
```

# Opaque Data

Create an instance of the `H5OpaqueInfo` type and pass it to the `H5Dataset` constructor to treat byte arrays as opaque data:

```cs
var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

var opaqueInfo = new H5OpaqueInfo(
    TypeSize: (uint)data.Length,
    Tag: "My tag"
);

var file = new H5File
{
    ["opaque"] = new H5Dataset(data, opaqueInfo: opaqueInfo)
};

file.Write("path/to/file.h5");
```

# Compound Data

HDF5 datasets of data type `compound` are created when the input data is a reference type (`class`) or a complex value (`struct`). By default, PureHDF writes only class properties and struct fields to the file.

This behavior can be changed by creating a new instance of the `H5WriteOptions` class with the desired values for `IncludeClassProperties`, `IncludeClassFields`, `IncludeStructProperties` and `IncludeStructFields`. The options should then be passed to the `H5File.Write(string, H5WriteOptions)` method.

## Field Name Mapping

You can control the mapping of .NET property names or field names to HDF5 compound field names by setting the appropriate values for `FieldNameMapper` or `PropertyNameMapper`, respectively, in the `H5WriteOptions`.

A name mapper is a function which takes a `PropertyInfo` or `FieldInfo`, respectively, and returns the desired name of the specific field in the HDF5 file.

You are free to derive a proper name from the input data but if you have control over the type definitions of the data to be written you can use the predefined `H5Name` attribute like this:

```cs
class MyClass
{
    [property: H5Name("my-name")]
    public double Y { get; set; }
}
```

And then define and use the property name mapper as follows:

```cs
string? PropertyNameMapper(PropertyInfo propertyInfo)
{
    var attribute = propertyInfo.GetCustomAttribute<H5NameAttribute>();
    return attribute is not null ? attribute.Name : default;
}

var options = new H5WriteOptions(
    PropertyNameMapper: propertyNameMapper
);

file.Write(filePath, options);
```
