using System.Text;

namespace PureHDF.VOL.Native;

internal class LocalHeap
{
    #region Fields

    private byte _version;
    private byte[]? _data;
    private readonly H5DriverBase _driver;

    #endregion

    #region Constructors

    public LocalHeap(H5Context context)
    {
        var (driver, superblock) = context;
        _driver = driver;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // version
        Version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // data segment size
        DataSegmentSize = superblock.ReadLength(driver);

        // free list head offset
        FreeListHeadOffset = superblock.ReadLength(driver);

        // data segment address
        DataSegmentAddress = superblock.ReadOffset(driver);
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("HEAP");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(LocalHeap)} are supported.");

            _version = value;
        }
    }

    public ulong DataSegmentSize { get; set; }
    public ulong FreeListHeadOffset { get; set; }
    public ulong DataSegmentAddress { get; set; }

    public byte[] Data
    {
        get
        {
            if (_data is null)
            {
                _driver.Seek((long)DataSegmentAddress, SeekOrigin.Begin);
                _data = _driver.ReadBytes((int)DataSegmentSize);
            }

            return _data;
        }
    }

    #endregion

    #region Methods

    public string GetObjectName(ulong offset)
    {
        var end = Array.IndexOf(Data, (byte)0, (int)offset);
        var bytes = Data[(int)offset..end];

        return Encoding.ASCII.GetString(bytes);
    }

    #endregion
}