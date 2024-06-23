Most data can simply be added to the `H5File` or `H5Group` dictionary like this:

```
var file = new H5File()
{
    ["my-dataset"] = data // data contains the simple or complex data you want to write
};
```

However, there are a few cases where a little bit more work is required. These are described below:

# Nullable Value Types

To write nullable value type data use the generic version of `H5Dataset` (or `H5Attribute`). If you use the non-generic one instead, you will get a compiler warning. It is necessary to use the generic `H5Dataset<T>` in combination with `null` data because `null` looses all type information at runtime and then PureHDF does not know how to encode it. The generic type parameter keeps that type information.

Example:

```
var file = new H5File()
{
    int? value = 1; // or null
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