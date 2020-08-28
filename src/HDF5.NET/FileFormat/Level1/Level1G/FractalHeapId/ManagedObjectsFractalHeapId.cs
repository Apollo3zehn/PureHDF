using System.IO;

namespace HDF5.NET
{
    public class ManagedObjectsFractalHeapId : FractalHeapId
    {
        #region Constructors

        public ManagedObjectsFractalHeapId(BinaryReader reader, ulong offsetByteCount, ulong lengthByteCount) 
            : base(reader)
        {
            this.Offset = H5Utils.ReadUlong(reader, offsetByteCount);
            this.Length = H5Utils.ReadUlong(reader, lengthByteCount);
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        #endregion
    }
}
