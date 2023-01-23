using System.Threading.Tasks.Sources;

namespace HDF5.NET
{
    internal abstract class H5BinaryReader : IDisposable
    {
        public abstract long Position { get; }
        public abstract long Length { get; }
        public ulong BaseAddress { get; set; }

        public abstract void Seek(long offset, SeekOrigin seekOrigin);
        public abstract int Read(Span<byte> buffer);
        public abstract ValueTask<int> ReadAsync(Memory<byte> buffer);
        public abstract byte ReadByte();
        public abstract byte[] ReadBytes(int count);
        public abstract ushort ReadUInt16();
        public abstract short ReadInt16();
        public abstract uint ReadUInt32();
        public abstract ulong ReadUInt64();
        public abstract void Dispose();
    }
}
