using System.IO;

namespace HDF5.NET
{
    public class ManagedObjectsFractalHeapId : FractalHeapId
    {
        #region Constructors

        public ManagedObjectsFractalHeapId(BinaryReader reader) : base(reader)
        {
#warning implement this correctly
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        protected override FractalHeapIdType ExpectedType => FractalHeapIdType.Managed;

        #endregion
    }
}
