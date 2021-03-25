namespace HDF5.NET
{
    internal abstract class LinkInfo : FileBlock
    {
        #region Constructors

        public LinkInfo(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
