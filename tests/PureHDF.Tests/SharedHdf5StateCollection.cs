using Xunit;

namespace PureHDF.Tests;

// Test classes that read HDF5 files (via PureHDF and/or the native HDF5 C
// library through HDF.PInvoke) touch a fair amount of process-global state:
//
//   * PureHDF's static NativeCache (file map + global-heap map keyed by driver)
//     and H5Filter.Registrations (notably mutated by H5Filter.ResetRegistrations()
//     in several filter tests).
//   * The native HDF5 C library's own global file-tracking state, exercised by
//     TestUtils.PrepareTestFile(...) when it shells out to H5F.create / H5F.close.
//
// xUnit's default behaviour runs each test class in its own implicit collection,
// and runs collections in parallel. When two of these classes happen to land on
// different threads at the same time, the global state above is racy enough that
// individual reads occasionally fail with errors like "process cannot access the
// file" on Windows, or stale filter registrations during a read.
//
// Test classes opted in to this collection serialise with each other and with
// any other test class also opted in. Classes outside the collection (e.g. the
// pure write-side tests) continue to run in parallel as before.
[CollectionDefinition(Name, DisableParallelization = true)]
public class SharedHdf5StateCollection
{
    public const string Name = "PureHDF shared static state";
}
