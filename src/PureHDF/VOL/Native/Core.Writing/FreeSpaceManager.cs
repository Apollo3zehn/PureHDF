namespace PureHDF.VOL.Native;

internal class FreeSpaceManager
{
    private long _length;

    public long Allocate(long length)
    {
        var address = _length;

        _length += length;

        return address;
    }
}