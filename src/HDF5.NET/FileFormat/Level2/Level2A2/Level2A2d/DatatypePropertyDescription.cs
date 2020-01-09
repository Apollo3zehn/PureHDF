using System.IO;

namespace HDF5.NET
{
    public abstract class DatatypePropertyDescription : FileBlock
    {
        public DatatypePropertyDescription(BinaryReader reader) : base(reader)
        {
            //
        }
    }
}
