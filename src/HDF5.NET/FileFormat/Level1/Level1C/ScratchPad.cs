namespace HDF5.NET
{
    internal abstract class ScratchPad : FileReader
    {
        #region Constructors

        public ScratchPad(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
