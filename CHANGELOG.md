## v1.0.0-alpha.25 - 2023-03-23

### Breaking Changes
- The property `group.Children` does not exist anymore. Use `group.Children()` instead. The reason is that properties do not work well in combination with the async programming model.

### Features
- Added support for the LZF filter.
- Virtual Object Layer (VOL)
  - There is now a VOL connector for native H5 files and a VOL connector for HSDS (see below).
  - Added experimental support for the [Highly Scalable Data Service (HSDS)](https://github.com/Apollo3zehn/PureHDF/tree/a1c690f642235c6975f805cb5750d1c75cd1a837#10-highly-scalable-data-service-hsds)
- Virtual File Driver (VFD)
  - Added support for [Amazon S3](https://github.com/Apollo3zehn/PureHDF/tree/a1c690f642235c6975f805cb5750d1c75cd1a837#9-amazon-s3) hosted files
- More async methods. 

> Note: Asynchronous methods of the `native VOL connector` (like `group.AttributesAsync()`) are not yet implemented asynchronously but synchronously. **Exception**: read methods like `dataset.ReadAsync(...)` are implemented asynchronously. In future all async methods will be truly async.

## v1.0.0-alpha.24 - 2023-02-10

### Bugs fixed
- Fixed a problen with the global heap (#28).

### Features
- Support for virtual datasets.
- PointSelection

## v1.0.0-alpha.23 - 2023-01-25

> Important: The project has been renamed from HDF5.NET to PureHDF.

### Bugs Fixed
- Fix the problem that the new async methods were not fully thread-safe.

### Features
- Added multithreaded read support for `H5File.Open(string)`, `H5File.Open(Stream)` (if `Stream` is `FileStream`) and `H5File.Open(MemoryMappedViewAccessor)`. See https://github.com/Apollo3zehn/PureHDF#8-concurrency for more details.

## v1.0.0-alpha.22 - 2023-01-20

### Bugs Fixed
- Fixed a bug which caused chunked .mat files to not be readable.

## v1.0.0-alpha.21 - 2023-01-06

### Features
- Async read methods added.
- Doc strings added on all public members for better intellisense support.

## v1.0.0-alpha.20 - 2023-01-03

### Features
- More type information is being exposed, especially compound members (name, offset, type) and the array base type.

## v1.0.0-alpha.19 - 2023-01-02

### Features
- Non generic version of ReadCompound supports reading unknown structs.

## v1.0.0-alpha.18 - 2022-12-13

### Features
- ReadCompound now supports nested arrays.
