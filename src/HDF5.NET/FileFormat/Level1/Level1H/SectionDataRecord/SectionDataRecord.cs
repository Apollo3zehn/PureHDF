using System.IO;

namespace HDF5.NET
{
    public abstract class SectionDataRecord : FileBlock
    {
        #region Constructors

        public SectionDataRecord(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
