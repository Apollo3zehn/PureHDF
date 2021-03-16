namespace HDF5.NET
{
    internal abstract class SectionDataRecord : FileBlock
    {
        #region Constructors

        public SectionDataRecord(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
