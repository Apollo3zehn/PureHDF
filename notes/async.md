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

# Notes
- Overview (1)
- The issue (2) which is the basis for the new .NET 6 API which allow async file reads using the `RandomAccess` class.
- It seems that (3) is not an issue anymore. I was unable to reproduce his findings with .NET 7.
- RandomAccess.ReadAsync(SafeFileHandle) also works when FileOptions.Asynchronous is not specified. But then it runs on a different thread instead of being really asynchronous (4)

1) https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/#scatter-gather-io
2) https://github.com/dotnet/runtime/issues/24847
3) https://sergeyteplyakov.github.io/Blog/concurrency/2019/05/29/Shooting-Yourself-in-the-Foot-with-Concurrent-Use-of-FileStream-Position.html
4) https://www.tabsoverspaces.com/233639-open-filestream-properly-for-asynchronous-reading-writing
