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
