using System.IO;

namespace HDF5.NET
{
    public abstract class LinkInfo : FileBlock
    {
        #region Constructors

        public LinkInfo(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
