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

    public byte[] Data
    {
        get
        {
            if (_data is null)
            {
                Driver.Seek((long)DataSegmentAddress, SeekOrigin.Begin);
                _data = Driver.ReadBytes((int)DataSegmentSize);
            }

            return _data;
        }
    }

    public static LocalHeap Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // data segment size
        var dataSegmentSize = superblock.ReadLength(driver);

        // free list head offset
        var freeListHeadOffset = superblock.ReadLength(driver);

        // data segment address
        var dataSegmentAddress = superblock.ReadOffset(driver);

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

    public string GetObjectName(ulong offset)
    {
        var end = Array.IndexOf(Data, (byte)0, (int)offset);
        var bytes = Data[(int)offset..end];

        return Encoding.UTF8.GetString(bytes);
    }
}