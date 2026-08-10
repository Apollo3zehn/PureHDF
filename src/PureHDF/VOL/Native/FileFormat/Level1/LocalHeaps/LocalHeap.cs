using System.Text;

namespace PureHDF.VOL.Native;

// CONCURRENCY / CACHING: immutable and context-free on purpose, so that a retained object-header
// message can cache one (see SymbolTableMessage, ObjectHeaderScratchPad, ExternalFileListMessage).
//
// The previous shape held an H5DriverBase and read the data segment lazily on first GetObjectName.
// That could not be cached for two independent reasons:
//
//   1. The driver is now a per-read-operation object handed back to NativeOperationSlot and reused,
//      so a retained heap holding one would read through an unrelated operation's cursor.
//   2. This was a `record struct` with a mutable `byte[]? _data` field and a non-readonly
//      GetObjectName. Caching it as a `LocalHeap?` and reading through `.Value` hands out a COPY
//      each time, so the lazy field would never be observed populated and the data segment would be
//      re-read on every single name lookup - silently, at no diagnostic cost.
//
// Reading the segment eagerly and being a class fixes both: there is no lazy state left to share and
// nothing to copy. It reads no more bytes than the lazy version did, because the lazy version also
// read the whole segment - just at an unpredictable moment.
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
