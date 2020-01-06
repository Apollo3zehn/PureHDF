using System.IO;

namespace HDF5.NET
{
    public abstract class BTree1Key : FileBlock
    {
        public BTree1Key(BinaryReader reader) : base(reader)
        {
            //
        }
    }
}
