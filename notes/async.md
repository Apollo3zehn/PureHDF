Is HDF5.NET always thread-safe? The following table analyzes the classes defined in `Reader.cs`.

| SafeFileHandle | .NET 6+ | Result | Comment |
| -------------- | ------- | ------ | ------- |
| Sync @ H5BinaryReader | 
| no             | -       | ✓      | When SafeFileHandle = null, async operations on H5BinaryReader are not supported. |
| yes            | no      | ✓      | Async operations are not available. |
| yes            | yes     | ✓      | Thread-safe RandomAccess class is used. |
||
| Sync @ H5Stream | 
| no             | -       | ✓      | When SafeFileHandle = null, async operations are only allowed on stack-only streams (1) |
| yes            | no      | ✓      | Async operations are not available. |
| yes            | yes     | ✓      | Thread-safe RandomAccess class is used. |
||
| Async @ H5BinaryReader | 
| no             | -       | ⓧ      | Async read operations are not supported. |
| yes            | no      | ✓      | Async operations are not available. |
| yes            | yes     | ✓      | Thread-safe RandomAccess class is used. |
||
| Async @ H5Stream | 
| no             | -       | ✓ / ⓧ  | When SafeFileHandle = null, async operations are only allowed on stack-only streams (1) |
| yes            | no      | ✓      | Async operations are not available. |
| yes            | yes     | ✓      | Thread-safe RandomAccess class is used. |

(1) The term `stack-only stream` means that a stream instance is created per `dataset.Read()` invocation. Since there is only one async operation per `dataset.Read()` invocation (i.e. only a single chunk is read at a time) and that operation is always directly awaited without any `stream.Seek()` operations in-between, thread-safety is not an issue here.