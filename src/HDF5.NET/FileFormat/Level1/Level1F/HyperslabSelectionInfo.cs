using System.IO;

namespace HDF5.NET
{
    public abstract class HyperslabSelectionInfo : FileBlock
    {
        public HyperslabSelectionInfo(BinaryReader reader) : base(reader)
        {
            //
        }
    }
}
