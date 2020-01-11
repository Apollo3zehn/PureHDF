using System.IO;

namespace HDF5.NET
{
    public abstract class StoragePropertyDescription : FileBlock
    {
        public StoragePropertyDescription(BinaryReader reader) : base(reader)
        {
            //
        }
    }
}
