# Filters

## Built-in

- Shuffle (hardware accelerated<sup>1</sup>, SSE2/AVX2)
- Fletcher32
- Deflate (zlib)
- Scale-Offset

## External

External filters must be registered to PureHDF before they can be used. You can register a filter by first installing the respective nuget package and then instantiate the filter class. If, for instance, you would like to add support for the `C-Blosc2` filter, run `dotnet add package PureHDF.Filters.Blosc2` to add the nuget package to your project and then register it:

```cs
using PureHDF.Filters;

H5Filter.Register(new Blosc2Filter());
```

If you would like to add your own filter implementation, just create a new class which derives from the `IH5Filter` interface as shown below. The `IH5Filter.GetParameters` method needs to be implemented only if the filter is used to `compress` data.

```cs
public class MyFilter : IH5Filter
{
    public ushort FilterId => <your filter ID>;
    public string Name => "<your filter name>";

    public Memory<byte> Filter(FilterInfo info)
    {
        throw new NotImplementedException();
    }

    public uint[] GetParameters(uint[] chunkDimensions, uint typeSize, Dictionary<string, object>? options)
    {
        throw new NotImplementedException();
    }
}
```

> [!NOTE]
> For `reading`, the filter will be selected automatically by the library as long as it is registered with the correct ID.

> [!NOTE]
> For `writing`, the filter will be selected only if registered properly and then explicitly specified for the dataset to write (or you add it to the `H5WriteOptions.Filters` list). See [writing](writing/filters.md) for more details.

## Supported filters overview

The first group of filters is built into PureHDF.

| Filter                                                                                      | Compress | Decompress | Notes                                                                                                             |
| ------------------------------------------------------------------------------------------- | -------- | ---------- | ----------------------------------------------------------------------------------------------------------------- |
| Shuffle                                                                                     | &check;  | &check;    | hardware-accelerated                                                                                              |
| Fletcher-32                                                                                 | &check;  | &check;    |                                                                                                                   |
| N-Bit                                                                                       | -        | -          |                                                                                                                   |
| Scale-Offset                                                                                | -        | &check;    |                                                                                                                   |
| Deflate                                                                                     | &check;  | &check;    | based on [ZLibStream](https://learn.microsoft.com/de-de/dotnet/api/system.io.compression.zlibstream?view=net-7.0) |
|                                                                                             |          |            |                                                                                                                   |
| [C-Blosc2](https://www.nuget.org/packages/PureHDF.Filters.Blosc2)                           | &check;  | &check;    | native, hardware-accelerated                                                                                      |
| [Bitshuffle](https://www.nuget.org/packages/PureHDF.Filters.Bitshuffle)                     | &check;  | &check;    | native, hardware-accelerated                                                                                      |
| [BZip2 (SharpZipLib)](https://www.nuget.org/packages/PureHDF.Filters.BZip2.SharpZipLib)     | &check;  | &check;    |                                                                                                                   |
| [Deflate (ISA-L)](https://www.nuget.org/packages/PureHDF.Filters.Deflate.ISA-L)             | &check;  | &check;    | native, hardware-accelerated                                                                                      |
| [Deflate (SharpZipLib)](https://www.nuget.org/packages/PureHDF.Filters.Deflate.SharpZipLib) | &check;  | &check;    |                                                                                                                   |
| [LZF](https://www.nuget.org/packages/PureHDF.Filters.LZF)                                   | &check;  | &check;    |                                                                                                                   |

## External Filter Details

**C-Blosc2**

- based on [Blosc2.PInvoke](https://www.nuget.org/packages/Blosc2.PInvoke)
- hardware accelerated: `SSE2` / `AVX2`

`dotnet add package PureHDF.Filters.Blosc2`

```cs
using PureHDF.Filters;

H5Filter.Register(new Blosc2Filter());
```

**Bitshuffle***

- based on [Bitshuffle.PInvoke](https://www.nuget.org/packages/Bitshuffle.PInvoke)
- hardware accelerated: `AVX2`

`dotnet add package PureHDF.Filters.Bitshuffle`

```cs
using PureHDF.Filters;

H5Filter.Register(new BitshuffleFilter());
```

**bzip2 (SharpZipLib)**

- based on [SharpZipLib](https://www.nuget.org/packages/SharpZipLib)

`dotnet add package PureHDF.Filters.BZip2.SharpZipLib`

```cs
using PureHDF.Filters;

H5Filter.Register(new BZip2SharpZipLibFilter());
```

**Deflate (Intel ISA-L)**

- based on [Intrinsics.ISA-L.PInvoke](https://www.nuget.org/packages/Intrinsics.ISA-L.PInvoke/)
- hardware accelerated: `SSE2` / `AVX2` / `AVX512`
- [benchmark results](https://github.com/Apollo3zehn/PureHDF/blob/master/benchmarks/PureHDF.Benchmarks/Inflate.md)

`dotnet add package PureHDF.Filters.Deflate.ISA-L`

```cs
using PureHDF.Filters;

H5Filter.Register(new DeflateISALFilter());
```

**Deflate (SharpZipLib)**

- based on [SharpZipLib](https://www.nuget.org/packages/SharpZipLib)

`dotnet add package PureHDF.Filters.Deflate.SharpZipLib`

```cs
using PureHDF.Filters;

H5Filter.Register(new DeflateSharpZipLibFilter());
```

**LZF**

`dotnet add package PureHDF.Filters.LZF`

```cs
using PureHDF.Filters;

H5Filter.Register(new LzfFilter());
```