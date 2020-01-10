using System.IO;

namespace HDF5.NET
{
    public abstract class BTree2Node : FileBlock
    {
        public BTree2Node(BinaryReader reader) : base(reader)
        {
            //
        }
    }
}
