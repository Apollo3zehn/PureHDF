using System.IO;

namespace HDF5.NET
{
    public abstract class BTree2Record : FileBlock
    {
        #region Constructors

        public BTree2Record(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
