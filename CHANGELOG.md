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
