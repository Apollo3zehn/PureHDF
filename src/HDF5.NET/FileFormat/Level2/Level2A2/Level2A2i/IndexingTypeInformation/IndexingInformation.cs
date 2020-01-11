using System.IO;

namespace HDF5.NET
{
    public abstract class IndexingInformation : FileBlock
    {
        public IndexingInformation(BinaryReader reader) : base(reader)
        {
            //
        }
    }
}
