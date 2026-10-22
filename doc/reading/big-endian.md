# Big-Endian Data

PureHDF converts non-native byte order while reading. This is most commonly needed for big-endian HDF5 files on little-endian machines.

Byte order is tracked on each datatype leaf, not on the whole file or dataset. That means mixed layouts are handled member by member:

- big-endian integer, floating-point and bitfield values are converted to the host byte order
- compound members can use different byte orders in the same element
- array datatypes and enumerations are converted through their base datatype
- variable-length sequence payloads are converted after the global-heap data has been resolved

For example, a dataset of `VL<big-endian int32>` can be read as `int[][]`; each sequence is resolved from the heap and its `int` payload is converted before it is returned.

Compound data with variable-length members must be read through a reference-capable target type, such as a class with an array property:

```csharp
private sealed class Row
{
    public int Id { get; set; }
    public int[] Values { get; set; } = default!;
}

var rows = dataset.Read<Row[]>();
```

If you need converted numeric values, do not read the dataset as raw bytes:

```csharp
var bytes = dataset.Read<byte[]>(); // raw file bytes, no byte-order conversion
var values = dataset.Read<int[]>(); // converted values
```

Raw byte reads are useful when the caller wants the serialized file representation. They return the file bytes unchanged, so a big-endian `int32` value `1` is returned as `00 00 00 01` on every machine.
