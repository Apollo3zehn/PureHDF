using System.Text;

namespace PureHDF.VOL.Native;

internal record struct LocalHeap(
    H5DriverBase Driver,
    ulong DataSegmentSize,
    ulong FreeListHeadOffset,
    ulong DataSegmentAddress
)
{
    private byte _version;

    private byte[]? _data;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("HEAP");

    public required byte Version
    {
        readonly get
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
        var freeListHeadOffset = await superblock.ReadLength(driver).ConfigureAwait(false);

        // data segment address
        var dataSegmentAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        return new LocalHeap(
            Driver: driver,
            DataSegmentSize: dataSegmentSize,
            FreeListHeadOffset: freeListHeadOffset,
            DataSegmentAddress: dataSegmentAddress
        )
        {
            Version = version
        };
    }

    public async ValueTask<string> GetObjectName(ulong offset)
    {
        if (_data is null)
        {
            Driver.SeekRelativeToBaseAddress((long)DataSegmentAddress);
            _data = await Driver.ReadBytes((int)DataSegmentSize).ConfigureAwait(false);
        }

        var end = Array.IndexOf(_data, (byte)0, (int)offset);
        var bytes = _data[(int)offset..end];

        return Encoding.UTF8.GetString(bytes);
    }
}