namespace PureHDF.VOL.Native;

internal class FreeSpaceManager
{
    private ulong _length;

    public ulong Allocate(ulong length)
    {
        var address = _length;

        _length += length;

        return address;
    }
}