## v1.0.0-beta.5 - 2024-02-02

### Bugs fixed
- Compounds with members not sorted by their offset can now be decoded correctly.

## v1.0.0-beta.4 - 2024-01-26

### Bugs fixed
- Files with many strings could not be written due to the new global heap collections no being created properly.

## v1.0.0-beta.3 - 2024-01-19

### Bugs fixed
- Attributes on a dataset are now also properly written to file.

## v1.0.0-beta.2 - 2023-09-15

### Features
- The `H5NativeWriter` now also exposes the associated `H5File` as property.

## v1.0.0-beta.1 - 2023-09-09

### Breaking Changes
- The reading API has been simplified to `T dataset.Read<T>(...)` and `T attribute.Read<T>(...)`. This means that method calls like `dataset.Read<T>` should be changed to `dataset.Read<T[]>`, otherwise you will get back a scalar value.
- The async implementation for the native VOL connector has been dropped and using it results in an exception. For the HSDS VOL connector, both APIs, sync and async, still work. The reason is that to avoid massive code duplication, we need to combine async and sync methods into a single asynchronous code path that can complete synchronous or asynchronous. This makes the code more general (good) but less optimized (bad), and much more complex because a lot of methods have to carry around the boolean parameter that decides whether the call is synchronous or asynchronous. Another reason for removing asynchronous support is that I don't see many use cases. Benchmarks have shown that multithreading is much more efficient on an M.2 SSD, and in my experience asynchronous code is most often useful for web servers to avoid creating many threads. Originally, async support was implemented to support running PureHDF in the browser as a web assembly module, since only async calls to a file stream are allowed in that environment. Hopefully this can be worked around by using web workers or other browser features.

### Features
- Write support to create new HDF5 files (no editing of existing files) has been added.

See https://apollo3zehn.github.io/PureHDF for more information about PureHDF features.

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
