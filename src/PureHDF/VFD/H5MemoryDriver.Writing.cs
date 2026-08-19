namespace PureHDF.VFD;

// The in-memory buffer driver is read-only by design: ReadOnlyMemory<byte> is not writable, and the
// write path needs a growable destination (see H5StreamDriver cursor mode + SetLength). A caller
// that wants to write an HDF5 image into memory should use the write-path memory overload, which
// owns a growable buffer internally and returns the written bytes; this driver is for reading an
// existing image. These throw to make that contract explicit if a write is ever routed here.
internal partial class H5MemoryDriver : H5DriverBase
{
    public override void Write(Span<byte> data)
    {
        throw new NotImplementedException();
    }

    public override void WriteDataset(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long endAddress)
    {
        throw new NotImplementedException();
    }
}
