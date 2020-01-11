using System.IO;

namespace HDF5.NET
{
    public class FractalHeapSharedMessageRecord : SharedMessageRecord
    {
        #region Constructors

        public FractalHeapSharedMessageRecord(BinaryReader reader) : base(reader)
        {
            // hash value
            this.HashValue = reader.ReadUInt32();

            // reference count
            this.ReferenceCount = reader.ReadUInt32();

            // fractal heap ID
            this.FractalHeapId = reader.ReadUInt64();
        }

        #endregion

        #region Properties

        public uint HashValue { get; set; }
        public uint ReferenceCount { get; set; }
        public ulong FractalHeapId { get; set; }

        #endregion
    }
}
