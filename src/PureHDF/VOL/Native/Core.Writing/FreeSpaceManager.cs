namespace PureHDF.VOL.Native;

internal class FreeSpaceManager
{
    private long _length;

    public long Allocate(long length)
    {
        if (length == 0)
            return Superblock.LongUndefinedAddress;

        var address = _length;

        _length += length;

        return address;
    }
}