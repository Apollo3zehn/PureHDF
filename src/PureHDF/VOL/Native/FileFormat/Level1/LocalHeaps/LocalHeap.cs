using System.Text;

namespace PureHDF.VOL.Native;

// CONCURRENCY / CACHING: immutable and context-free on purpose, so that a retained object-header
// message can cache one (see SymbolTableMessage, ObjectHeaderScratchPad, ExternalFileListMessage).
//
// Two constraints drive the shape:
//
//   1. The driver is a per-read-operation object handed back to NativeOperationSlot and reused,
//      so a retained heap holding one would read through an unrelated operation's cursor. Holding
//      no driver avoids that.
//   2. A value type with a mutable field cannot be cached: reading through a `LocalHeap?` via
//      `.Value` hands out a copy each time, so a lazily populated data field would never be observed
//      populated and the data segment would be re-read on every name lookup — silently, at no
//      diagnostic cost.
//
// Reading the segment eagerly in the constructor and being a class satisfies both: there is no
// lazy state to share and nothing to copy. The eager read costs no more bytes than a lazy one
// would, because the whole segment is needed eventually — the difference is only when.
internal sealed record class LocalHeap(byte[] Data)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("HEAP");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(LocalHeap)} are supported.");

            _version = value;
        }
    }

    public static async ValueTask<LocalHeap> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // reserved
        await driver.ReadBytes(3).ConfigureAwait(false);

        // data segment size
        var dataSegmentSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // free list head offset
        _ = await superblock.ReadLength(driver).ConfigureAwait(false);

        // data segment address
        var dataSegmentAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // data segment
        driver.SeekRelativeToBaseAddress((long)dataSegmentAddress);
        var data = await driver.ReadBytes((int)dataSegmentSize).ConfigureAwait(false);

        return new LocalHeap(data)
        {
            Version = version
        };
    }

    // Synchronous, because the bytes are already here. Every caller was `await`-ing a method that
    // usually completed synchronously anyway; NodeCompare3 in particular runs once per b-tree
    // comparison, so this removes a state machine from the inner loop of a name lookup.
    public string GetObjectName(ulong offset)
    {
        var end = Array.IndexOf(Data, (byte)0, (int)offset);

        return Encoding.UTF8.GetString(Data.AsSpan((int)offset..end));
    }
}
