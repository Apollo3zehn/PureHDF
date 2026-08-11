# Backlog

Work identified and deliberately not done, with enough measurement attached that picking it up does
not mean re-deriving the problem.

## The writer never builds a link name index, so groups it writes are always compact

**Impact:** every by-name link lookup in a group written by PureHDF is O(n) in the number of links.
A group with 2,000 links costs one full scan of 2,000 header messages per lookup, and a name that
does not exist costs the same as one that does.

**What happens.** A group can store its links two ways. *Compactly*, as one Link Message per link
inside the group's own object header; or *densely*, in a fractal heap indexed by a v2 B-tree on the
link name. The reader supports both: `NativeGroup.TryGetReference` takes the dense path when
`LinkInfoMessage.BTree2NameIndexAddress` is defined, and that path is a proper B-tree descent
(`BTree2Header.TryFindRecord`), so it is O(log n).

The writer never produces the dense form. `BTree2NameIndexAddress` is always undefined, so every
group falls to the compact branch no matter how many links it has:

```csharp
var linkMessage = header
    .GetMessages<LinkMessage>()
    .FirstOrDefault(message => message.LinkName == name);
```

That is a linear scan. Confirmed by instrumenting the branches: a 1,000-link group written by
`H5File.Write` reports 1,001 header messages and takes the compact branch on every lookup.

**Measured** (one `LinkExists` on a group of empty subgroups, PureHDF writer, after the
`TryWalkPath` re-decode fix - so this is the residual cost, all of it in-memory scanning):

| links | file size | bytes read per lookup | messages scanned |
|------:|----------:|----------------------:|-----------------:|
|    10 |     852 B |                     0 |               11 |
|   100 |   7,152 B |                     0 |              101 |
| 1,000 |  70,152 B |                     0 |            1,001 |
| 2,000 | 140,152 B |                     0 |            2,001 |

Note the reads are zero. This is no longer an I/O problem - the object header is decoded once and
cached, and the read-ahead window covers it. What remains is CPU and allocation, linear in link
count.

**Worth knowing before starting:** the HDF5 C library also scans compact links linearly, so the
reader's behaviour is not wrong - it is faithful. The gap is on the write side, and the C library
switches storage at a threshold (`H5Pset_link_phase_change`, default 8 compact / 6 dense). Matching
that would mean implementing fractal heap writing plus a v2 B-tree name index, which is
substantially more work than the reader-side fix that preceded this entry, and is arguably a change
upstream should own rather than this fork.

**Why it was found:** a benchmark (`MetadataRead.Warm_LookupLinksByName`) showed 261 KB allocated per
by-name lookup. Most of that turned out to be a separate defect - `TryWalkPath` re-decoding the
group's own object header on every lookup, since fixed and guarded by `LinkLookupCostTests`. This
entry is what is left after that.

## A virtual dataset can only reach external sources on the local filesystem, and fails silently

**Impact:** a virtual dataset read through a stream - an HTTP range-request stream, or any
non-filesystem source - silently returns FILL VALUES for every region backed by an external source,
instead of the data or an error. Sources inside the same file work correctly.

**What happens.** `VirtualDatasetStream.GetDatasetInfoAsync` resolves each source entry like this:

```csharp
var filePath = FilePathUtils.FindExternalFileForVirtualDataset(_file.FolderPath, entry.SourceFileName, _datasetAccess);

if (filePath is not null)
{
    var file = filePath == "."
        ? _file                                   // same file - fine, reuses the open file
        : await H5File.OpenReadAsync(filePath);    // external - local path only
    ...
}
```

`FindExternalFileForVirtualDataset` probes candidate paths with `File.Exists` and the result is opened
by path. There is no hook to supply a stream, no URI support and nothing pluggable, so an external
source is reachable only if it is a real file on a real filesystem.

Two consequences, and the second is the dangerous one:

1. `_file.FolderPath` comes from `Path.GetDirectoryName(absoluteFilePath)`, and a stream-opened file
   passes `absoluteFilePath: string.Empty`. So for a stream-backed file, resolution has no folder to
   search relative to and finds nothing.
2. Not finding a source returns `null`, and the caller treats `null` exactly like "no source covers
   this region" - it fills with the fill value. **An unreachable source is indistinguishable from a
   legitimately empty one.** Nothing is logged and nothing throws.

**What a fix looks like.** A resolution hook on `H5DatasetAccess` - something shaped like
`Func<string, ValueTask<Stream>>?` - so a caller can serve source files from wherever they live, with
the current `File.Exists` probing as the default. Separately, and independently useful: distinguish
"no source mapped here" from "source mapped but unreachable", and make the latter throw or at least be
observable rather than silently producing plausible data.

**Not a blocker for the async work.** The gather itself is fully asynchronous - `ReadVirtualAsync`
takes `Memory<T>` and awaits both source resolution and the source reads - so a virtual dataset whose
sources live in its own file reads correctly through a suspending stream today, and that path is
covered by `AsyncDatasetReadTests.ReadAsyncOfAVirtualDatasetWorksWhenEveryReadSuspends`. It is only
the cross-file case that cannot work, and it cannot work for reasons that have nothing to do with
async.

## Known defects left as found

Neither is introduced by this fork; both are present in upstream v2.1.4.

- **Tiny fractal heap IDs decode from the wrong source.** In
  `TinyObjectsFractalHeapIdSubType1.Read<T>`, every callback ignores its `driver` parameter and
  decodes from its own context instead, so a tiny heap ID reads from the file cursor rather than
  from the ID's inline `Data`. Left alone because changing it means changing the callback contract
  at all `FractalHeapId.Read` call sites.
- **`AttributeInfoMessage.BTree2CreationOrder` seeks the wrong address.** It seeks
  `BTree2CreationOrderIndexAddress` but is only reachable on a path that has already established the
  name index is the one to use, so the method is currently unreachable. `LinkInfoMessage` has the
  same shape.
- **`AmazonS3Stream` slot batching miscounts and leaks response streams.** The second
  `LoadFromS3ToCacheAndBufferAsync` call computes `s3EndIndex` from `s3StartIndex + length /
  slotSize` rather than from the index actually reached, and `ReadDataFromS3Async` returns
  `response.ResponseStream` without ever disposing the response. Out of scope here - this fork does
  not use the S3 VFD - but it is a real leak for anyone who does.
