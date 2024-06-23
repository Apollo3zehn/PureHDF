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