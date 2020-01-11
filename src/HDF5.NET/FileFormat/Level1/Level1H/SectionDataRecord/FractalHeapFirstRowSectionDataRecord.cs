using System.IO;

namespace HDF5.NET
{
    public class FractalHeapSingleSectionDataRecord : FractalHeapIndirectSectionDataRecord
    {
        #region Constructors

        public FractalHeapSingleSectionDataRecord(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
