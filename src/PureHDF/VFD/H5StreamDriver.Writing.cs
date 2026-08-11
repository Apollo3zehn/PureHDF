namespace PureHDF.VFD;

internal partial class H5StreamDriver : H5DriverBase
{
    // A read-ahead window would go stale the moment a write lands inside the range it holds, so the
    // reads the writer interleaves with its writes (the read-backs mentioned in the constructor)
    // could be served bytes the file no longer contains.
    //
    // That cannot currently happen: the writer constructs this driver with allowPositionless: false,
    // so it is in cursor mode and _readAhead is null. The invalidation below is here so that the
    // invariant does not depend on remembering it - if positionless writing is ever allowed, this is
    // already correct rather than silently wrong.
    public override void Write(Span<byte> data)
    {
        _readAhead?.Invalidate();
        _stream.Write(data);
    }

    public override void WriteDataset(Span<byte> buffer)
    {
        _readAhead?.Invalidate();
        _stream.Write(buffer);
    }

    public override void SetLength(long endAddress)
    {
        _stream.SetLength(endAddress);
    }
}