namespace PureHDF.VOL.Native;

internal abstract partial record class Superblock(
    byte Version,
    FileConsistencyFlags FileConsistencyFlags,
    ulong BaseAddress,
    ulong EndOfFileAddress
)
{
    public static byte[] FormatSignature { get; } = new byte[] { 0x89, 0x48, 0x44, 0x46, 0x0d, 0x0a, 0x1a, 0x0a };

    public static ulong UndefinedAddress { get; } = 0xFFFFFFFFFFFFFFFF;

    private byte _offsetsSize;

    private byte _lengthsSize;

    public required byte OffsetsSize
    {
        get
        {
            return _offsetsSize;
        }
        set
        {
            if (!(1 <= value && value <= 8 && Utils.IsPowerOfTwo(value)))
                throw new NotSupportedException("Superblock offsets size must be a power of two and in the range of 1..8.");

            _offsetsSize = value;
        }
    }

    public required byte LengthsSize
    {
        get
        {
            return _lengthsSize;
        }
        set
        {
            if (!(1 <= value && value <= 8 && Utils.IsPowerOfTwo(value)))
                throw new NotSupportedException("Superblock lengths size must be a power of two and in the range of 1..8.");

            _lengthsSize = value;
        }
    }

    public bool IsUndefinedAddress(ulong address)
    {
        return OffsetsSize switch
        {
            1 => (address & 0x00000000000000FF) == 0x00000000000000FF,
            2 => (address & 0x000000000000FFFF) == 0x000000000000FFFF,
            3 => (address & 0x0000000000FFFFFF) == 0x0000000000FFFFFF,
            4 => (address & 0x00000000FFFFFFFF) == 0x00000000FFFFFFFF,
            5 => (address & 0x000000FFFFFFFFFF) == 0x000000FFFFFFFFFF,
            6 => (address & 0x0000FFFFFFFFFFFF) == 0x0000FFFFFFFFFFFF,
            7 => (address & 0x00FFFFFFFFFFFFFF) == 0x00FFFFFFFFFFFFFF,
            8 => (address & 0xFFFFFFFFFFFFFFFF) == 0xFFFFFFFFFFFFFFFF,
            _ => throw new FormatException("The offset size byte count must be in the range of 1..8")
        };
    }

    public ulong ReadOffset(H5DriverBase driver)
    {
        return Utils.ReadUlong(driver, OffsetsSize);
    }

    public ulong ReadLength(H5DriverBase driver)
    {
        return Utils.ReadUlong(driver, LengthsSize);
    }
}
