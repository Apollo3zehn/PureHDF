namespace HDF5.NET
{
    internal abstract class H5BaseReader : Stream
    {
        public H5BaseReader(long length)
        {
            Length = length;
        }

        #region H5BinaryReader

        public ulong BaseAddress { get; set; }

#if !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP3_0_OR_GREATER
        public abstract int Read(Span<byte> buffer);
#endif

        public abstract ValueTask<int> ReadAsync(Memory<byte> buffer);
        public abstract new byte ReadByte();
        public abstract byte[] ReadBytes(int count);
        public abstract ushort ReadUInt16();
        public abstract short ReadInt16();
        public abstract uint ReadUInt32();
        public abstract ulong ReadUInt64();

        #endregion

        #region Stream

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;

        public override abstract long Position { get; set; }
        public override long Length { get; }

        public override abstract long Seek(long offset, SeekOrigin seekOrigin);

        // ReadAsync: https://devblogs.microsoft.com/pfxteam/overriding-stream-asynchrony/
        // see "If you don’t override ..."
        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Flush() => throw new NotImplementedException();

        #endregion
    }
}
